using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using AetherXIV.Core.Map.dataobjects;

using AetherXIV.Core.Common;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.actors.chara.ai;

namespace AetherXIV.Core.Map
{
    class Server
    {
        public const int FFXIV_MAP_PORT = 54992;
        public const int BUFFER_SIZE = 0xFFFF; //Max basepacket size is 0xFFFF
        public const int BACKLOG = 100;
        private const uint SESSION_END_GRACE_SECONDS = 5;

        public const string STATIC_ACTORS_PATH = "./staticactors.bin";

        private static Server mSelf;

        private Socket mServerSocket;
        private int mStopping;

        private Dictionary<uint, Session> mSessionList = new Dictionary<uint, Session>();        
     
        private static CommandProcessor mCommandProcessor = new CommandProcessor();
        private static ZoneConnection mWorldConnection = new ZoneConnection();
        private static WorldManager mWorldManager;
        private static Dictionary<uint, ItemData> mGamedataItems;
        private static Dictionary<uint, GuildleveData> mGamedataGuildleves;
        private static StaticActors mStaticActors;

        private PacketProcessor mProcessor;        

        public Server()
        {
            mSelf = this;
        }
        
        public bool StartServer()
        {
            Interlocked.Exchange(ref mStopping, 0);
            mStaticActors = new StaticActors(STATIC_ACTORS_PATH);

            mGamedataItems = Database.GetItemGamedata();
            Program.Log.Info("Loaded {0} items.", mGamedataItems.Count);
            mGamedataGuildleves = Database.GetGuildleveGamedata();
            Program.Log.Info("Loaded {0} guildleves.", mGamedataGuildleves.Count);

            mWorldManager = new WorldManager(this);
            mWorldManager.LoadZoneList();
            mWorldManager.LoadZoneEntranceList();
            mWorldManager.LoadSeamlessBoundryList();
            mWorldManager.LoadActorClasses();
            mWorldManager.LoadSpawnLocations();
            mWorldManager.LoadBattleNpcs();
            mWorldManager.LoadStatusEffects();
            mWorldManager.LoadBattleCommands();
            mWorldManager.LoadBattleTraits();
            mWorldManager.SpawnAllActors();
            mWorldManager.StartZoneThread();

            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(ConfigConstants.OPTIONS_BINDIP), int.Parse(ConfigConstants.OPTIONS_PORT));

            try
            {
                mServerSocket = new Socket(serverEndPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
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
            Program.Log.Info("Map Server has started @ {0}:{1}", (mServerSocket.LocalEndPoint as IPEndPoint).Address, (mServerSocket.LocalEndPoint as IPEndPoint).Port);
            Console.ForegroundColor = ConsoleColor.Gray;

            mProcessor = new PacketProcessor(this);
            StartupReadySignal.TryWrite("Map", String.Format("{0}:{1}", (mServerSocket.LocalEndPoint as IPEndPoint).Address, (mServerSocket.LocalEndPoint as IPEndPoint).Port));

            //mGameThread = new Thread(new ThreadStart(mProcessor.update));
            //mGameThread.Start();
            return true;
        }

        public void StopServer()
        {
            if (Interlocked.Exchange(ref mStopping, 1) != 0)
                return;

            Program.Log.Info("Map Server graceful shutdown started.");
            DevDiagnostics.Trace(
                "service.shutdown.map.started",
                "sessions", mSessionList.Count);

            CloseSocket(mServerSocket);
            mServerSocket = null;

            bool zoneLoopStopped = mWorldManager == null || mWorldManager.StopZoneThread(5000);
            if (!zoneLoopStopped)
                Program.Log.Warn("Zone Loop did not quiesce within 5 seconds; continuing character persistence.");

            List<Session> sessions = new List<Session>(mSessionList.Values);
            int saved = 0;
            int failed = 0;
            foreach (Session session in sessions)
            {
                if (session == null || session.isEnding)
                    continue;

                session.BeginEnding();
                try
                {
                    session.GetActor().CleanupAndSave();
                    saved++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Program.Log.Error(ex, "Failed to persist session {0} during Map shutdown.", session.id);
                    DevDiagnostics.Trace(
                        "service.shutdown.map.saveFailed",
                        "session", session.id,
                        "player", session.GetActor().customDisplayName,
                        "error", ex.Message);
                }
            }

            mSessionList.Clear();
            if (mWorldConnection != null)
                CloseSocket(mWorldConnection.socket);
            mWorldConnection = null;

            DevDiagnostics.Trace(
                "service.shutdown.map.completed",
                "saved", saved,
                "failed", failed,
                "zoneLoopStopped", zoneLoopStopped);
            Program.Log.Info(
                "Map Server graceful shutdown completed: saved={0}, failed={1}, zoneLoopStopped={2}.",
                saved,
                failed,
                zoneLoopStopped);
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

        #region Session Handling

        public Session AddSession(uint id)
        {
            PruneEndingSessions();

            if (mSessionList.ContainsKey(id))
            {
                if (mSessionList[id].isEnding)
                    RemoveSession(id);
                else
                {
                    mSessionList[id].ClearInstance();
                    return mSessionList[id];
                }
            }

            Session session = new Session(id);
            mSessionList.Add(id, session);
            return session;
        }

        public void BeginSessionEnd(uint id)
        {
            if (mSessionList.ContainsKey(id))
                mSessionList[id].BeginEnding();
        }

        public void RemoveSession(uint id)
        {
            if (mSessionList.ContainsKey(id))
            {
                mSessionList.Remove(id);                
            }
        }

        public void PruneEndingSessions()
        {
            uint now = Utils.UnixTimeStampUTC();
            List<uint> expiredSessions = new List<uint>();

            foreach (KeyValuePair<uint, Session> entry in mSessionList)
            {
                if (entry.Value.IsEndingExpired(now, SESSION_END_GRACE_SECONDS))
                    expiredSessions.Add(entry.Key);
            }

            foreach (uint sessionId in expiredSessions)
                RemoveSession(sessionId);
        }

        public Session GetSession(uint id)
        {
            if (mSessionList.ContainsKey(id))
                return mSessionList[id];
            else
                return null;
        }

        public Session GetSession(string name)
        {
            foreach (Session s in mSessionList.Values)
            {
                if (s.GetActor().customDisplayName.ToLower().Equals(name.ToLower()))
                    return s;
            }
            return null;
        }

        public Dictionary<uint, Session> GetSessionList()
        {
            return mSessionList;
        }

        #endregion

        #region Socket Handling
        private void AcceptCallback(IAsyncResult result)
        {
            if (IsStopping())
                return;

            ZoneConnection conn = null;
            Socket socket = (System.Net.Sockets.Socket)result.AsyncState;

            try
            {

                conn = new ZoneConnection();
                conn.socket = socket.EndAccept(result);
                conn.buffer = new byte[BUFFER_SIZE];

                mWorldConnection = conn;
                
                Program.Log.Info("Connection {0}:{1} has connected.", (conn.socket.RemoteEndPoint as IPEndPoint).Address, (conn.socket.RemoteEndPoint as IPEndPoint).Port);
                //Queue recieving of data from the connection
                conn.socket.BeginReceive(conn.buffer, 0, conn.buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), conn);
                //Queue the accept of the next incomming connection
                if (!IsStopping())
                    mServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), mServerSocket);
            }
            catch (SocketException)
            {
                if (conn != null)
                {
                    mWorldConnection = null;
                }
                if (!IsStopping())
                    mServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), mServerSocket);
            }
            catch (ObjectDisposedException) when (IsStopping())
            {
            }
            catch (Exception) when (!IsStopping())
            {
                if (conn != null)
                {
                    mWorldConnection = null;
                }
                if (!IsStopping())
                    mServerSocket.BeginAccept(new AsyncCallback(AcceptCallback), mServerSocket);
            }
        }
        
        /// <summary>
        /// Receive Callback. Reads in incoming data, converting them to base packets. Base packets are sent to be parsed. If not enough data at the end to build a basepacket, move to the beginning and prepend.
        /// </summary>
        /// <param name="result"></param>
        private void ReceiveCallback(IAsyncResult result)
        {
            if (IsStopping())
                return;

            ZoneConnection conn = (ZoneConnection)result.AsyncState;

            try
            {
                // Poll can race the graceful shutdown socket close, so keep it
                // inside the same disposal-safe block as EndReceive.
                if ((conn.socket.Poll(1, SelectMode.SelectRead) && conn.socket.Available == 0))
                {
                    mWorldConnection = null;
                    Program.Log.Info("Disconnected from world server!");
                    return;
                }

                int bytesRead = conn.socket.EndReceive(result);

                bytesRead += conn.lastPartialSize;

                if (bytesRead >= 0)
                {
                    int offset = 0;

                    //Build packets until can no longer or out of data
                    while (true)
                    {
                        SubPacket subPacket = SubPacket.CreatePacket(ref offset, conn.buffer, bytesRead);

                        //If can't build packet, break, else process another
                        if (subPacket == null)
                            break;
                        else
                            mProcessor.ProcessPacket(conn, subPacket);
                    }

                    //Not all bytes consumed, transfer leftover to beginning
                    if (offset < bytesRead)
                        Array.Copy(conn.buffer, offset, conn.buffer, 0, bytesRead - offset);

                    conn.lastPartialSize = bytesRead - offset;

                    //Build any queued subpackets into basepackets and send
                    conn.FlushQueuedSendPackets();
                    PruneEndingSessions();

                    if (offset < bytesRead)
                        //Need offset since not all bytes consumed
                        conn.socket.BeginReceive(conn.buffer, bytesRead - offset, conn.buffer.Length - (bytesRead - offset), SocketFlags.None, new AsyncCallback(ReceiveCallback), conn);
                    else
                        //All bytes consumed, full buffer available
                        conn.socket.BeginReceive(conn.buffer, 0, conn.buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), conn);
                }
                else
                {
                    mWorldConnection = null;
                    Program.Log.Info("Disconnected from world server!");
                }
            }
            catch (SocketException)
            {
                if (conn.socket != null)
                {
                    mWorldConnection = null;
                    Program.Log.Info("Disconnected from world server!");
                }
            }
            catch (ObjectDisposedException) when (IsStopping())
            {
            }
        }

        #endregion

        public static ZoneConnection GetWorldConnection()
        {
            return mWorldConnection;
        }

        public static Server GetServer()
        {
            return mSelf;
        }

        public static CommandProcessor GetCommandProcessor()
        {
            return mCommandProcessor;
        }        

        public static WorldManager GetWorldManager()
        {
            return mWorldManager;
        }
        
        public static Dictionary<uint, ItemData> GetGamedataItems()
        {
            return mGamedataItems;
        }

        public static Actor GetStaticActors(uint id)
        {
            Actor actor = mStaticActors.GetActor(id);
            if (actor != null)
                return actor;

            return GetBattleCommandActor(id);
        }

        public static Actor GetStaticActors(string name)
        {
            return mStaticActors.FindStaticActor(name);
        }

        private static Actor GetBattleCommandActor(uint id)
        {
            if ((id & 0xFFF00000) != 0xA0F00000 || mWorldManager == null)
                return null;

            BattleCommand battleCommand = mWorldManager.GetBattleCommand(id & 0xFFFF);
            if (battleCommand == null)
                return null;

            string commandScript = GetBattleCommandScriptName(battleCommand);
            if (commandScript == null)
                return null;

            return new Command(id, commandScript);
        }

        private static string GetBattleCommandScriptName(BattleCommand battleCommand)
        {
            switch (battleCommand.commandType)
            {
                case CommandType.Spell:
                    return "EffectMagic";
                case CommandType.Ability:
                    return "Ability";
                case CommandType.WeaponSkill:
                    return "AttackWeaponSkill";
                default:
                    return null;
            }
        }

        public static ItemData GetItemGamedata(uint id)
        {
            if (mGamedataItems.ContainsKey(id))
                return mGamedataItems[id];
            else
                return null;
        }

        public static GuildleveData GetGuildleveGamedata(uint id)
        {
            if (mGamedataGuildleves.ContainsKey(id))
                return mGamedataGuildleves[id];
            else
                return null;
        }

    }
}
