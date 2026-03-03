using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Infrastructure.Configuration;
using Foundry.Storage.Infrastructure.Scanning;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Foundry.Storage.Tests.Infrastructure;

public sealed class ClamAvFileScannerTests
{
    [Fact]
    public async Task ScanAsync_WhenFileIsClean_ReturnsCleanResult()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: OK");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "clean file content"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "clean.txt");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeTrue();
        result.ThreatName.Should().BeNull();
    }

    [Fact]
    public async Task ScanAsync_WhenFileIsInfected_ReturnsInfectedResult()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: Win.Test.EICAR_HDB-1 FOUND");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "infected content"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "virus.exe");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Be("Win.Test.EICAR_HDB-1");
    }

    [Fact]
    public async Task ScanAsync_WhenUnexpectedResponse_ReturnsInfectedWithUnknownThreat()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: ERROR something went wrong");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "some content"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "mystery.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Contain("Unknown threat");
    }

    [Fact]
    public async Task ScanAsync_WhenConnectionRefused_ThrowsException()
    {
        // Use a port that nothing is listening on
        ClamAvFileScanner scanner = CreateScanner(1);

        byte[] content = "content"u8.ToArray();
        MemoryStream stream = new MemoryStream(content);

        try
        {
            Func<Task> act = () => scanner.ScanAsync(stream, "file.txt");
            await act.Should().ThrowAsync<SocketException>();
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task ScanAsync_StreamsLargeFileCorrectly()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: OK", captureData: true);
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        // Create file larger than ChunkSize (8192)
        byte[] largeContent = new byte[20000];
        RandomNumberGenerator.Fill(largeContent);
        using MemoryStream stream = new MemoryStream(largeContent);

        FileScanResult result = await scanner.ScanAsync(stream, "large-file.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeTrue();
        server.ReceivedData.Should().BeEquivalentTo(largeContent);
    }

    private static ClamAvFileScanner CreateScanner(int port)
    {
        IOptions<StorageOptions> options = Options.Create(new StorageOptions
        {
            ClamAvHost = "127.0.0.1",
            ClamAvPort = port
        });
        return new ClamAvFileScanner(options, NullLogger<ClamAvFileScanner>.Instance);
    }

    private sealed class FakeClamAvServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serverTask;
        private readonly bool _captureData;
        private readonly List<byte> _receivedData = [];

        public int Port { get; }
        public byte[] ReceivedData => _receivedData.ToArray();

        public FakeClamAvServer(string response, bool captureData = false)
        {
            _captureData = captureData;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _serverTask = RunAsync(response);
        }

        public async Task WaitForCompletionAsync()
        {
            await _serverTask;
        }

        private async Task RunAsync(string response)
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync();
            NetworkStream ns = client.GetStream();

            if (_captureData)
            {
                await ReadChunkedDataAsync(ns);
            }
            else
            {
                await DrainInputAsync(ns);
            }

            byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\0");
            await ns.WriteAsync(responseBytes);
        }

        private async Task ReadChunkedDataAsync(NetworkStream ns)
        {
            // Read command exactly (zINSTREAM\0 = 10 bytes)
            byte[] cmdBuf = new byte[10];
            await ReadExactlyAsync(ns, cmdBuf, 10);

            // Read chunked data until zero-length terminator
            while (true)
            {
                byte[] lengthBuf = new byte[4];
                await ReadExactlyAsync(ns, lengthBuf, 4);

                int chunkLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuf, 0));
                if (chunkLen == 0)
                {
                    break;
                }

                byte[] chunkBuf = new byte[chunkLen];
                await ReadExactlyAsync(ns, chunkBuf, chunkLen);
                _receivedData.AddRange(chunkBuf);
            }
        }

        private static async Task ReadExactlyAsync(NetworkStream ns, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int bytesRead = await ns.ReadAsync(buffer.AsMemory(read, count - read));
                if (bytesRead == 0)
                {
                    break;
                }
                read += bytesRead;
            }
        }

        private static async Task DrainInputAsync(NetworkStream ns)
        {
            byte[] buffer = new byte[65536];
            try
            {
                while (true)
                {
                    int bytesRead = await ns.ReadAsync(buffer);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    if (!ns.DataAvailable)
                    {
                        await Task.Delay(50);
                        if (!ns.DataAvailable)
                        {
                            break;
                        }
                    }
                }
            }
            catch (IOException)
            {
                // Client may have closed
            }
        }

        public void Dispose()
        {
            _listener.Stop();
            _listener.Dispose();
        }
    }
}
