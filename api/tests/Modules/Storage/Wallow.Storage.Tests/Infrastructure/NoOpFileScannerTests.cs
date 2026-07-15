using Microsoft.Extensions.Logging;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Infrastructure.Scanning;

namespace Wallow.Storage.Tests.Infrastructure;

public sealed class NoOpFileScannerTests
{
    private readonly NoOpFileScanner _scanner;
    private readonly ILogger<NoOpFileScanner> _logger;

    public NoOpFileScannerTests()
    {
        _logger = Substitute.For<ILogger<NoOpFileScanner>>();
        _scanner = new NoOpFileScanner(_logger);
    }

    [Fact]
    public async Task ScanAsync_ReturnsClean()
    {
        using MemoryStream stream = new(new byte[] { 1, 2, 3 });

        FileScanResult result = await _scanner.ScanAsync(stream, "test.txt");

        result.IsClean.Should().BeTrue();
        result.ThreatName.Should().BeNull();
    }

    [Fact]
    public async Task ScanAsync_WithEmptyStream_ReturnsClean()
    {
        using MemoryStream stream = new();

        FileScanResult result = await _scanner.ScanAsync(stream, "empty.txt");

        result.IsClean.Should().BeTrue();
    }

    [Fact]
    public async Task ScanAsync_SupportsCancellation()
    {
        using MemoryStream stream = new(new byte[] { 1 });
        using CancellationTokenSource cts = new();

        FileScanResult result = await _scanner.ScanAsync(stream, "test.txt", cts.Token);

        result.IsClean.Should().BeTrue();
    }
}
