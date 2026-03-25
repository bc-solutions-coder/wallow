using System.Buffers;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Infrastructure.Configuration;

namespace Wallow.Storage.Infrastructure.Scanning;

public sealed partial class ClamAvFileScanner : IFileScanner
{
    private readonly StorageOptions _options;
    private readonly ILogger<ClamAvFileScanner> _logger;

    private const int ChunkSize = 8192;
    private const int MaxResponseSize = 4096;

    public ClamAvFileScanner(IOptions<StorageOptions> options, ILogger<ClamAvFileScanner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FileScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        using TcpClient client = new();
        await client.ConnectAsync(_options.ClamAvHost, _options.ClamAvPort, cancellationToken);

        await using NetworkStream stream = client.GetStream();

        // Send INSTREAM command
        byte[] command = "zINSTREAM\0"u8.ToArray();
        await stream.WriteAsync(command, cancellationToken);

        // Stream file in chunks with 4-byte big-endian length prefix
        byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        try
        {
            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, ChunkSize), cancellationToken)) > 0)
            {
                byte[] lengthPrefix = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(bytesRead));
                await stream.WriteAsync(lengthPrefix, cancellationToken);
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        // Send zero-length chunk to signal end of stream
        byte[] terminator = new byte[4];
        await stream.WriteAsync(terminator, cancellationToken);

        // Read response
        byte[] responseBuffer = new byte[MaxResponseSize];
        int responseLength = await stream.ReadAsync(responseBuffer, cancellationToken);
        string response = Encoding.UTF8.GetString(responseBuffer, 0, responseLength).TrimEnd('\0', '\n', '\r');

        LogScanResult(fileName, response);

        // Response format: "stream: OK" or "stream: <threat> FOUND"
        if (response.EndsWith("OK", StringComparison.OrdinalIgnoreCase))
        {
            return FileScanResult.Clean();
        }

        if (response.EndsWith("FOUND", StringComparison.OrdinalIgnoreCase))
        {
            // Extract threat name from "stream: <threat> FOUND"
            int colonIndex = response.IndexOf(':', StringComparison.Ordinal);
            int foundIndex = response.LastIndexOf(" FOUND", StringComparison.OrdinalIgnoreCase);
            if (colonIndex >= 0 && foundIndex > colonIndex)
            {
                string threatName = response[(colonIndex + 1)..foundIndex].Trim();
                LogThreatDetected(threatName, fileName);
                return FileScanResult.Infected(threatName);
            }
        }

        LogUnexpectedResponse(fileName, response);
        return FileScanResult.Infected($"Unknown threat (response: {response})");
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "ClamAV scan result for {FileName}: {Response}")]
    private partial void LogScanResult(string fileName, string response);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ClamAV detected threat {ThreatName} in file {FileName}")]
    private partial void LogThreatDetected(string threatName, string fileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unexpected ClamAV response for {FileName}: {Response}")]
    private partial void LogUnexpectedResponse(string fileName, string response);
}
