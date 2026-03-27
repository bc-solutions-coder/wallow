using Microsoft.Playwright;

namespace Wallow.E2E.Tests.Fixtures;

[Trait("Category", "E2E")]
public sealed class PlaywrightFixtureTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture = new();

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task CreateBrowserContextAsync_WithRecordVideoFalse_ReturnsContextWithNoVideoDir()
    {
        IBrowserContext context = await _fixture.CreateBrowserContextAsync(recordVideo: false);

        Assert.NotNull(context);

        IPage page = await context.NewPageAsync();
        Assert.Null(page.Video);

        await page.CloseAsync();
        await context.DisposeAsync();
    }

    [Fact]
    public async Task CreateBrowserContextAsync_WithRecordVideoTrue_ReturnsContextWithVideoDir()
    {
        string? originalValue = Environment.GetEnvironmentVariable("E2E_VIDEO");
        try
        {
            Environment.SetEnvironmentVariable("E2E_VIDEO", "true");

            IBrowserContext context = await _fixture.CreateBrowserContextAsync(recordVideo: true);

            Assert.NotNull(context);

            IPage page = await context.NewPageAsync();
            Assert.NotNull(page.Video);

            await page.CloseAsync();
            await context.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("E2E_VIDEO", originalValue);
        }
    }
}
