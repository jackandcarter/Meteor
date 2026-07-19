using System.Net;
using System.Net.Sockets;
using AetherXIV.Core;
using AetherXIV.Server.Hosting;

namespace AetherXIV.Server.Hosting.Tests;

public sealed class TcpServerListenerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task OnConnectionObservesExactBytesSentByClient()
    {
        byte[] sent = [1, 2, 3, 4, 5];
        TaskCompletionSource<byte[]> received = new(TaskCreationOptions.RunContinuationsAsynchronously);

        TcpServerListener listener = new(
            new ServerRuntimeOptions("test", new ServerEndpoint("127.0.0.1", 0)),
            async (connection, ct) =>
            {
                using MemoryStream buffer = new();
                while (true)
                {
                    System.IO.Pipelines.ReadResult result = await connection.Input.ReadAsync(ct);
                    foreach (System.ReadOnlyMemory<byte> segment in result.Buffer)
                        buffer.Write(segment.Span);

                    connection.Input.AdvanceTo(result.Buffer.End);
                    if (result.IsCompleted)
                        break;
                }

                received.TrySetResult(buffer.ToArray());
            });

        using CancellationTokenSource cts = new();
        Task loopTask = listener.RunAsync(cts.Token).AsTask();

        IPEndPoint boundEndpoint = await listener.Started.WaitAsync(Timeout);

        using (TcpClient client = new())
        {
            await client.ConnectAsync(boundEndpoint.Address, boundEndpoint.Port).WaitAsync(Timeout);
            await using NetworkStream stream = client.GetStream();
            await stream.WriteAsync(sent).AsTask().WaitAsync(Timeout);
        }

        byte[] observed = await received.Task.WaitAsync(Timeout);
        Assert.Equal(sent, observed);

        cts.Cancel();
        await loopTask.WaitAsync(Timeout);
    }

    [Fact]
    public async Task DataWrittenToOutputReachesTheClient()
    {
        byte[] banner = "hello"u8.ToArray();

        TcpServerListener listener = new(
            new ServerRuntimeOptions("test", new ServerEndpoint("127.0.0.1", 0)),
            async (connection, ct) =>
            {
                await connection.Output.WriteAsync(banner, ct);
                await connection.Output.FlushAsync(ct);
            });

        using CancellationTokenSource cts = new();
        Task loopTask = listener.RunAsync(cts.Token).AsTask();

        IPEndPoint boundEndpoint = await listener.Started.WaitAsync(Timeout);

        byte[] observed;
        using (TcpClient client = new())
        {
            await client.ConnectAsync(boundEndpoint.Address, boundEndpoint.Port).WaitAsync(Timeout);
            await using NetworkStream stream = client.GetStream();

            observed = new byte[banner.Length];
            int totalRead = 0;
            while (totalRead < observed.Length)
            {
                int read = await stream.ReadAsync(observed.AsMemory(totalRead)).AsTask().WaitAsync(Timeout);
                if (read == 0)
                    break;

                totalRead += read;
            }
        }

        Assert.Equal(banner, observed);

        cts.Cancel();
        await loopTask.WaitAsync(Timeout);
    }

    [Fact]
    public async Task CancellingTokenStopsTheLoopAndTheListener()
    {
        TcpServerListener listener = new(
            new ServerRuntimeOptions("test", new ServerEndpoint("127.0.0.1", 0)),
            (_, _) => ValueTask.CompletedTask);

        using CancellationTokenSource cts = new();
        Task loopTask = listener.RunAsync(cts.Token).AsTask();

        IPEndPoint boundEndpoint = await listener.Started.WaitAsync(Timeout);

        cts.Cancel();
        await loopTask.WaitAsync(Timeout);

        await Assert.ThrowsAnyAsync<SocketException>(async () =>
        {
            using TcpClient client = new();
            await client.ConnectAsync(boundEndpoint.Address, boundEndpoint.Port).WaitAsync(Timeout);
        });
    }
}
