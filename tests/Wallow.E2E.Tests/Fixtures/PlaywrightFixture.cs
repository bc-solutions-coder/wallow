using Microsoft.Playwright;

namespace Wallow.E2E.Tests.Fixtures;

public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();

        bool headed = bool.TryParse(Environment.GetEnvironmentVariable("E2E_HEADED"), out bool h) && h;
        float? slowMo = int.TryParse(Environment.GetEnvironmentVariable("E2E_SLOWMO"), out int ms) ? ms : null;

        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !headed,
            SlowMo = slowMo,
        });
    }

    public async Task<IBrowserContext> CreateBrowserContextAsync(bool recordVideo = false)
    {
        BrowserNewContextOptions? options = null;

        if (recordVideo && Environment.GetEnvironmentVariable("E2E_VIDEO") is not null)
        {
            options = new BrowserNewContextOptions
            {
                RecordVideoDir = "test-results/videos",
            };
        }

        return await Browser.NewContextAsync(options);
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }
}
