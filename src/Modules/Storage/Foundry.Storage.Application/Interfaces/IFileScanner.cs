namespace Foundry.Storage.Application.Interfaces;

public interface IFileScanner
{
    Task<FileScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}

public record FileScanResult(bool IsClean, string? ThreatName)
{
    public static FileScanResult Clean() => new(true, null);
    public static FileScanResult Infected(string threatName) => new(false, threatName);
}
