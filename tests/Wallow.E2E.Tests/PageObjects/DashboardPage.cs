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
        try
        {
            await _page.Locator("[data-testid='apps-heading']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<string?> GetWelcomeMessageAsync()
    {
        ILocator heading = _page.Locator("[data-testid='apps-heading']");
        bool isVisible = await heading.IsVisibleAsync();
        if (!isVisible)
        {
            return null;
        }

        return await heading.InnerTextAsync();
    }
}
