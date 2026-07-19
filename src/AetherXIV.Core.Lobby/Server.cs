using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Lobby
{
    class Server
    {
        public const int FFXIV_LOBBY_PORT   = 54994;
        public const int BUFFER_SIZE        = 0xFFFF;
        public const int BACKLOG            = 100;

        private const int CLEANUP_THREAD_SLEEP_TIME = 60;

        private Socket mServerSocket;
        private int mStopping;
        private List<ClientConnection> mConnectionList = new List<ClientConnection>();
        private PacketProcessor mProcessor;

        private Thread cleanupThread;
        private bool killCleanupThread = false;

        private void SocketCleanup()
        {
            Program.Log.Info("Cleanup thread started; it will run every {0} seconds.", CLEANUP_THREAD_SLEEP_TIME);
            while (!killCleanupThread)
            {
                int count = 0;
                for (int i = mConnectionList.Count - 1; i >= 0; i--)
                {
                    ClientConnection conn = mConnectionList[i];
                    if (conn.socket.Poll(1, SelectMode.SelectRead) && conn.socket.Available == 0)
                    {
                        conn.socket.Disconnect(false);
                        mConnectionList.Remove(conn);
                        count++;
                    }
                }
                if (count != 0)
                    Program.Log.Info("{0} connections were cleaned up.", count);
                Thread.Sleep(CLEANUP_THREAD_SLEEP_TIME*1000);
            }
        }

        #region Socket Handling
        public bool StartServer()
        {
            Interlocked.Exchange(ref mStopping, 0);
            //cleanupThread = new Thread(new ThreadStart(socketCleanup));
            //cleanupThread.Name = "LobbyThread:Cleanup";
            //cleanupThread.Start();

            IPEndPoint serverEndPoint = new System.Net.IPEndPoint(IPAddress.Parse(ConfigConstants.OPTIONS_BINDIP), int.Parse(ConfigConstants.OPTIONS_PORT));
           
            try{
                mServerSocket = new System.Net.Sockets.Socket(serverEndPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);                
            }
            catch (Exception e)
            {
                throw new ApplicationException("Could not Create socket, check to make sure not duplicating port", e);
            }
            try
            {
                mServerSocket.Bind(serverEndPoint);
                mServerSocket.Listen(BACKLOG);
            }
            catch (Exception e)
            {
                throw new ApplicationException("Error occured while binding socket, check inner exception", e);
            }
            try
            {               
                mServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), mServerSocket);
            }
            catch (Exception e)
            {
                throw new ApplicationException("Error occured starting listeners, check inner exception", e);
            }

            Console.ForegroundColor = ConsoleColor.White;
            Program.Log.Info("Lobby Server has started @ {0}:{1}", (mServerSocket.LocalEndPoint as IPEndPoint).Address, (mServerSocket.LocalEndPoint as IPEndPoint).Port);
            Console.ForegroundColor = ConsoleColor.Gray;

            mProcessor = new PacketProcessor();
            StartupReadySignal.TryWrite("Lobby", String.Format("{0}:{1}", (mServerSocket.LocalEndPoint as IPEndPoint).Address, (mServerSocket.LocalEndPoint as IPEndPoint).Port));

            return true;
        }

        public void StopServer()
        {
            if (Interlocked.Exchange(ref mStopping, 1) != 0)
                return;

            Program.Log.Info("Lobby Server graceful shutdown started.");
            DevDiagnostics.Trace(
                "service.shutdown.lobby.started",
                "connections", mConnectionList.Count);

            CloseSocket(mServerSocket);
            mServerSocket = null;

            List<ClientConnection> connections;
            lock (mConnectionList)
            {
                connections = new List<ClientConnection>(mConnectionList);
                mConnectionList.Clear();
            }

            foreach (ClientConnection connection in connections)
                connection.Disconnect();

            killCleanupThread = true;
            if (cleanupThread != null && cleanupThread.IsAlive)
                cleanupThread.Join(1000);

            DevDiagnostics.Trace(
                "service.shutdown.lobby.completed",
                "connectionsClosed", connections.Count);
            Program.Log.Info(
                "Lobby Server graceful shutdown completed: connectionsClosed={0}.",
                connections.Count);
        }

        private bool IsStopping()
        {
            return Volatile.Read(ref mStopping) != 0;
        }

        private static void CloseSocket(Socket socket)
        {
            if (socket == null)
                return;

            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                socket.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void AcceptCallback(IAsyncResult result)
        {
            if (IsStopping())
                return;

            ClientConnection conn = null;
            try
            {
                System.Net.Sockets.Socket s = (System.Net.Sockets.Socket)result.AsyncState;
                conn = new ClientConnection();
                conn.socket = s.EndAccept(result);
                conn.buffer = new byte[BUFFER_SIZE];
                lock (mConnectionList)
                {
                    mConnectionList.Add(conn);
                }
                //Queue recieving of data from the connection
                conn.socket.BeginReceive(conn.buffer, 0, conn.buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), conn);
                //Queue the accept of the next incomming connection
                if (!IsStopping())
                    mServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), mServerSocket);
                Program.Log.Info("Connection {0}:{1} has connected.", (conn.socket.RemoteEndPoint as IPEndPoint).Address, (conn.socket.RemoteEndPoint as IPEndPoint).Port);
            }
            catch (SocketException)
            {
                if (conn.socket != null)
                {
                    conn.socket.Close();
                    lock (mConnectionList)
                    {
                        mConnectionList.Remove(conn);
                    }
                }
                if (!IsStopping())
                    mServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), mServerSocket);
            }
            catch (ObjectDisposedException) when (IsStopping())
            {
            }
            catch (Exception) when (!IsStopping())
            {
                if (conn.socket != null)
                {
                    conn.socket.Close();
                    lock (mConnectionList)
                    {
                        mConnectionList.Remove(conn);
                    }
                }
                if (!IsStopping())
                    mServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), mServerSocket);
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            if (IsStopping())
                return;

            ClientConnection conn = (ClientConnection)result.AsyncState;            

            try
            {
                int bytesRead = conn.socket.EndReceive(result);
                int socketBytesRead = bytesRead;
                int bufferedBytes = conn.lastPartialSize;

                bytesRead += conn.lastPartialSize;

                if (bytesRead > 0)
                {
                    Program.Log.Info(
                        "{0} receive socketBytes={1} bufferedBytes={2} totalBuffered={3}",
                        GetConnectionName(conn),
                        socketBytesRead,
                        bufferedBytes,
                        bytesRead);
                    PacketDiagnostics.LogBufferPreview(
                        "Lobby",
                        "receive " + GetConnectionName(conn),
                        conn.buffer,
                        0,
                        bytesRead);

                    int offset = 0;

                    //Build packets until can no longer or out of data
                    while (true)
                    {
                        BasePacket basePacket = BuildPacket(ref offset, conn.buffer, bytesRead);

                        //If can't build packet, break, else process another
                        if (basePacket == null)
                            break;
                        else
                        {
                            PacketDiagnostics.LogBasePacket("Lobby", "recv " + GetConnectionName(conn), basePacket);
                            mProcessor.ProcessPacket(conn, basePacket);
                        }
                    }

                    if (offset < bytesRead)
                    {
                        Program.Log.Info(
                            "{0} has {1} unconsumed bytes after packet build.",
                            GetConnectionName(conn),
                            bytesRead - offset);
                        PacketDiagnostics.LogBufferPreview(
                            "Lobby",
                            "partial " + GetConnectionName(conn),
                            conn.buffer,
                            offset,
                            bytesRead - offset);
                    }

                    //Not all bytes consumed, transfer leftover to beginning
                    if (offset < bytesRead)
                        Array.Copy(conn.buffer, offset, conn.buffer, 0, bytesRead - offset);

                    Array.Clear(conn.buffer, bytesRead - offset, conn.buffer.Length - (bytesRead - offset));

                    conn.lastPartialSize = bytesRead - offset;

                    //Build any queued subpackets into basepackets and send
                    conn.FlushQueuedSendPackets();

                    if (offset < bytesRead)
                        //Need offset since not all bytes consumed
                        conn.socket.BeginReceive(conn.buffer, bytesRead - offset, conn.buffer.Length - (bytesRead - offset), SocketFlags.None, new AsyncCallback(ReceiveCallback), conn);
                    else
                        //All bytes consumed, full buffer available
                        conn.socket.BeginReceive(conn.buffer, 0, conn.buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), conn);
                }
                else
                {
                    Program.Log.Info(
                        "{0} has disconnected. lastPartialSize={1}",
                        GetConnectionName(conn),
                        conn.lastPartialSize);
                    if (conn.lastPartialSize > 0)
                        PacketDiagnostics.LogBufferPreview(
                            "Lobby",
                            "disconnect-partial " + GetConnectionName(conn),
                            conn.buffer,
                            0,
                            conn.lastPartialSize);

                    lock (mConnectionList)
                    {
                        conn.Disconnect();
                        mConnectionList.Remove(conn);
                    }
                }
            }
            catch (SocketException)
            {
                if (!IsStopping() && conn.socket != null)
                {
                    Program.Log.Info(
                        "{0} has disconnected after socket exception. lastPartialSize={1}",
                        GetConnectionName(conn),
                        conn.lastPartialSize);
                    if (conn.lastPartialSize > 0)
                        PacketDiagnostics.LogBufferPreview(
                            "Lobby",
                            "socket-exception-partial " + GetConnectionName(conn),
                            conn.buffer,
                            0,
                            conn.lastPartialSize);

                    lock (mConnectionList)
                    {
                        mConnectionList.Remove(conn);
                    }
                }
            }
            catch (ObjectDisposedException) when (IsStopping())
            {
            }
        }      

        private string GetConnectionName(ClientConnection conn)
        {
            if (conn == null)
                return "Unknown";

            if (conn.currentUserId != 0)
                return "User " + conn.currentUserId;

            try
            {
                return conn.GetAddress();
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Builds a packet from the incoming buffer + offset. If a packet can be built, it is returned else null.
        /// </summary>
        /// <param name="offset">Current offset in buffer.</param>
        /// <param name="buffer">Incoming buffer.</param>
        /// <returns>Returns either a BasePacket or null if not enough data.</returns>
        public BasePacket BuildPacket(ref int offset, byte[] buffer, int bytesRead)
        {
            BasePacket newPacket = null;

            //Too small to even get length
            if (bytesRead <= offset)
                return null;

            ushort packetSize = BitConverter.ToUInt16(buffer, offset);

            //Too small to whole packet
            if (bytesRead < offset + packetSize)
                return null;

            if (buffer.Length < offset + packetSize)
                return null;

            try
            {
                newPacket = new BasePacket(buffer, ref offset);
            }
            catch (OverflowException)
            {
                return null;
            }

            return newPacket;
        }

        #endregion

    }
}
