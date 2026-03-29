using Microsoft.Extensions.Logging;
using Wallow.Storage.Application.Interfaces;

namespace Wallow.Storage.Infrastructure.Scanning;

public sealed partial class NoOpFileScanner(ILogger<NoOpFileScanner> logger) : IFileScanner
{
    public Task<FileScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        LogScanSkipped(fileName);
        return Task.FromResult(FileScanResult.Clean());
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Virus scanning disabled — skipping scan for {FileName}")]
    private partial void LogScanSkipped(string fileName);
}
