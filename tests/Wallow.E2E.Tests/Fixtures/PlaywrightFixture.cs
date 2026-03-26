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

    public async Task<IBrowserContext> CreateBrowserContextAsync()
    {
        return await Browser.NewContextAsync();
    }

    // Legacy: will be removed when existing tests migrate to E2ETestBase
    public static async Task WaitForBlazorAsync(IPage page, int timeoutMs = 10_000)
    {
        await page.WaitForFunctionAsync(
            """
            () => {
                if (typeof Blazor === 'undefined') return false;
                const perf = performance.getEntriesByType('resource');
                return perf.some(e => e.name.includes('_blazor'));
            }
            """,
            null,
            new() { Timeout = timeoutMs });

        await page.WaitForTimeoutAsync(1000);
    }

    // Legacy: will be removed when existing tests migrate to E2ETestBase
    public static async Task ClickAndWaitForNavigationAsync(
        IPage page, string buttonSelector, Func<string, bool> urlPredicate,
        int maxAttempts = 3, int waitPerAttemptMs = 10_000)
    {
        ILocator button = page.Locator(buttonSelector);

        int buttonCount = await button.CountAsync();
        if (buttonCount == 0)
        {
            string content = await page.ContentAsync();
            string snippet = content.Length > 1000 ? content[..1000] : content;
            throw new InvalidOperationException(
                $"Button '{buttonSelector}' not found on page. URL: {page.Url}. Content: {snippet}");
        }

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            string urlBefore = page.Url;
            await button.ClickAsync(new() { Timeout = 5_000 });

            try
            {
                await page.WaitForURLAsync(
                    url => url != urlBefore && urlPredicate(url),
                    new() { Timeout = waitPerAttemptMs });
                return;
            }
            catch (TimeoutException) when (attempt < maxAttempts)
            {
                await page.WaitForTimeoutAsync(1000);
            }
        }

        string finalContent = await page.ContentAsync();
        string finalSnippet = finalContent.Length > 500 ? finalContent[..500] : finalContent;
        throw new TimeoutException(
            $"Button '{buttonSelector}' click did not trigger expected navigation after {maxAttempts} attempts. " +
            $"URL: {page.Url}. Content: {finalSnippet}");
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
