using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class LogoutPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public LogoutPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/logout");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.Locator("[data-testid='logout-confirm-heading']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task ConfirmLogoutAsync()
    {
        await _page.Locator("[data-testid='logout-confirm-button']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<string?> GetHeadingTextAsync()
    {
        ILocator heading = _page.Locator("[data-testid='logout-confirm-heading']");
        bool isVisible = await heading.IsVisibleAsync();
        if (!isVisible)
        {
            return null;
        }

        return await heading.InnerTextAsync();
    }
}
