using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Infrastructure.Configuration;
using Wallow.Storage.Infrastructure.Scanning;

namespace Wallow.Storage.Tests.Infrastructure;

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
    public async Task ScanAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: OK", delayMs: 2000);
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "content"u8.ToArray();
        MemoryStream stream = new MemoryStream(content);
        CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        try
        {
            Func<Task> act = () => scanner.ScanAsync(stream, "file.txt", cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            cts.Dispose();
            await stream.DisposeAsync();
        }
    }

    [Fact]
    public async Task ScanAsync_WithEmptyFile_ReturnsCleanResult()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: OK", captureData: true);
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        using MemoryStream stream = new MemoryStream([]);

        FileScanResult result = await scanner.ScanAsync(stream, "empty.txt");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeTrue();
        server.ReceivedData.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithExactChunkSizeFile_StreamsCorrectly()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: OK", captureData: true);
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        // Exactly 8192 bytes (ChunkSize) — should produce exactly one chunk
        byte[] content = new byte[8192];
        RandomNumberGenerator.Fill(content);
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "exact-chunk.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeTrue();
        server.ReceivedData.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task ScanAsync_WhenFoundResponseHasNoColon_ReturnsUnknownThreat()
    {
        // "FOUND" at end but no colon — colonIndex will be -1, falls through to unknown
        using FakeClamAvServer server = new FakeClamAvServer("malware FOUND");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "bad"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "no-colon.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Contain("Unknown threat");
        result.ThreatName.Should().Contain("malware FOUND");
    }

    [Fact]
    public async Task ScanAsync_WhenResponseOkIsCaseInsensitive_ReturnsClean()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: ok");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "content"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "lower-ok.txt");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_WhenResponseFoundIsCaseInsensitive_ReturnsInfected()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: Trojan.Generic found");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "content"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "case-found.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Be("Trojan.Generic");
    }

    [Fact]
    public async Task ScanAsync_WhenResponseHasNullAndNewlineTerminators_TrimsCorrectly()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: OK\0\n\r");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "content"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "trimmed.txt");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_WhenFoundColonAfterFound_ReturnsUnknownThreat()
    {
        // Edge case: colon exists but after " FOUND" — foundIndex <= colonIndex
        using FakeClamAvServer server = new FakeClamAvServer("FOUND: extra");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "data"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "edge.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Contain("Unknown threat");
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

    [Fact]
    public async Task ScanAsync_WhenFoundResponseHasColonButNoSpaceBeforeFound_ReturnsUnknownThreat()
    {
        // ":FOUND" — colonIndex=0, LastIndexOf(" FOUND")=-1, falls through to unknown
        using FakeClamAvServer server = new FakeClamAvServer(":FOUND");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "data"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "no-space-found.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Contain("Unknown threat");
        result.ThreatName.Should().Contain(":FOUND");
    }

    [Fact]
    public async Task ScanAsync_WhenInstreamSizeLimitExceeded_ReturnsUnknownThreat()
    {
        using FakeClamAvServer server = new FakeClamAvServer("INSTREAM size limit exceeded. ERROR");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "data"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "too-large.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Contain("Unknown threat");
        result.ThreatName.Should().Contain("INSTREAM size limit exceeded");
    }

    [Fact]
    public async Task ScanAsync_WhenServerClosesConnectionImmediately_HandlesEmptyResponse()
    {
        // Empty response — doesn't end with OK or FOUND, falls to unknown
        using FakeClamAvServer server = new FakeClamAvServer("");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "data"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "empty-response.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Contain("Unknown threat");
    }

    [Fact]
    public async Task ScanAsync_SendsZInstreamCommand()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: OK", captureData: true, captureCommand: true);
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "test"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "cmd-test.txt");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeTrue();
        server.ReceivedCommand.Should().Be("zINSTREAM\0");
    }

    [Fact]
    public async Task ScanAsync_SendsZeroLengthTerminatorAfterData()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: OK", captureData: true, captureTerminator: true);
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "test data"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "terminator-test.txt");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeTrue();
        server.ReceivedTerminator.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_WithMultipleChunks_SendsCorrectLengthPrefixes()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: OK", captureData: true, captureChunkSizes: true);
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        // 8192 + 4000 = 12192 bytes — should produce 2 chunks
        byte[] content = new byte[12192];
        RandomNumberGenerator.Fill(content);
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "multi-chunk.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeTrue();
        server.ReceivedChunkSizes.Should().HaveCount(2);
        server.ReceivedChunkSizes[0].Should().Be(8192);
        server.ReceivedChunkSizes[1].Should().Be(4000);
    }

    [Fact]
    public async Task ScanAsync_WhenResponseIsOnlyWhitespace_ReturnsUnknownThreat()
    {
        using FakeClamAvServer server = new FakeClamAvServer("   ");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "data"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "whitespace.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Contain("Unknown threat");
    }

    [Fact]
    public async Task ScanAsync_WhenClean_WithLoggingEnabled_LogsScanResult()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: OK");
        (ClamAvFileScanner scanner, ILoggerFactory loggerFactory) = CreateScannerWithLogging(server.Port);
        using IDisposable __ = loggerFactory;

        byte[] content = "clean content"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "log-clean.txt");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_WhenInfected_WithLoggingEnabled_LogsThreatDetected()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: Eicar-Test FOUND");
        (ClamAvFileScanner scanner, ILoggerFactory loggerFactory) = CreateScannerWithLogging(server.Port);
        using IDisposable __ = loggerFactory;

        byte[] content = "infected"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "log-infected.exe");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Be("Eicar-Test");
    }

    [Fact]
    public async Task ScanAsync_WhenUnexpectedResponse_WithLoggingEnabled_LogsUnexpectedResponse()
    {
        using FakeClamAvServer server = new FakeClamAvServer("UNKNOWN RESPONSE FORMAT");
        (ClamAvFileScanner scanner, ILoggerFactory loggerFactory) = CreateScannerWithLogging(server.Port);
        using IDisposable __ = loggerFactory;

        byte[] content = "data"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "log-unexpected.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Contain("Unknown threat");
    }

    [Fact]
    public async Task ScanAsync_WhenFoundWithEmptyThreatName_ReturnsTrimmedThreatName()
    {
        // "stream:  FOUND" — threat name after colon and before FOUND is just whitespace
        using FakeClamAvServer server = new FakeClamAvServer("stream:  FOUND");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "data"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "empty-threat.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WithOneByteFile_StreamsSingleChunk()
    {
        using FakeClamAvServer server = new FakeClamAvServer("stream: OK", captureData: true);
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = [0x42];
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "one-byte.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeTrue();
        server.ReceivedData.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task ScanAsync_WhenResponseEndsWithFoundButColonAfterFoundMarker_ReturnsUnknownThreat()
    {
        // "no-colon-before FOUND" — ends with FOUND, no colon at all → colonIndex=-1
        // This is similar to WhenFoundResponseHasNoColon but verifies the colonIndex < 0 branch
        using FakeClamAvServer server = new FakeClamAvServer("virus FOUND");
        ClamAvFileScanner scanner = CreateScanner(server.Port);

        byte[] content = "data"u8.ToArray();
        using MemoryStream stream = new MemoryStream(content);

        FileScanResult result = await scanner.ScanAsync(stream, "no-colon2.bin");
        await server.WaitForCompletionAsync();

        result.IsClean.Should().BeFalse();
        result.ThreatName.Should().Contain("Unknown threat");
    }

    private static ClamAvFileScanner CreateScanner(int port)
    {
        IOptions<StorageOptions> options = Options.Create(new StorageOptions
        {
            ClamAv = new ClamAvOptions { Host = "127.0.0.1", Port = port }
        });
        return new ClamAvFileScanner(options, NullLogger<ClamAvFileScanner>.Instance);
    }

    private static (ClamAvFileScanner Scanner, ILoggerFactory Factory) CreateScannerWithLogging(int port)
    {
        IOptions<StorageOptions> options = Options.Create(new StorageOptions
        {
            ClamAv = new ClamAvOptions { Host = "127.0.0.1", Port = port }
        });
        ILoggerFactory factory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Trace).AddSimpleConsole());
        return (new ClamAvFileScanner(options, factory.CreateLogger<ClamAvFileScanner>()), factory);
    }

    private sealed class FakeClamAvServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serverTask;
        private readonly bool _captureData;
        private readonly bool _captureCommand;
        private readonly bool _captureChunkSizes;
        private readonly int _delayMs;
        private readonly List<byte> _receivedData = [];
        private readonly List<int> _receivedChunkSizes = [];
        private string? _receivedCommand;
        private bool _receivedTerminator;

        public int Port { get; }
        public byte[] ReceivedData => _receivedData.ToArray();
        public string? ReceivedCommand => _receivedCommand;
        public bool ReceivedTerminator => _receivedTerminator;
        public List<int> ReceivedChunkSizes => _receivedChunkSizes;

        public FakeClamAvServer(
            string response,
            bool captureData = false,
            int delayMs = 0,
            bool captureCommand = false,
            bool captureTerminator = false,
            bool captureChunkSizes = false)
        {
            _captureData = captureData || captureCommand || captureTerminator || captureChunkSizes;
            _captureCommand = captureCommand;
            _captureChunkSizes = captureChunkSizes;
            _delayMs = delayMs;
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

            if (_delayMs > 0)
            {
                await Task.Delay(_delayMs);
            }

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

            if (_captureCommand)
            {
                _receivedCommand = Encoding.UTF8.GetString(cmdBuf);
            }

            // Read chunked data until zero-length terminator
            while (true)
            {
                byte[] lengthBuf = new byte[4];
                await ReadExactlyAsync(ns, lengthBuf, 4);

                int chunkLen = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuf, 0));
                if (chunkLen == 0)
                {
                    _receivedTerminator = true;
                    break;
                }

                if (_captureChunkSizes)
                {
                    _receivedChunkSizes.Add(chunkLen);
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
