using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class DashboardPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public DashboardPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/dashboard/apps");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsLoadedAsync()
    {
        ILocator heading = _page.Locator("h1:has-text('My Apps')");
        return await heading.IsVisibleAsync();
    }

    public async Task<string?> GetWelcomeMessageAsync()
    {
        // The dashboard shows "My Apps" as the main heading
        ILocator heading = _page.Locator("h1");
        bool isVisible = await heading.IsVisibleAsync();
        if (!isVisible)
        {
            return null;
        }

        return await heading.InnerTextAsync();
    }
}
