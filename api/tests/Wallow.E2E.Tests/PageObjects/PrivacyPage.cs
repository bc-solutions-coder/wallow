using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class PrivacyPage
{
    private readonly IPage _page;
    private readonly string _authBaseUrl;

    public PrivacyPage(IPage page, string authBaseUrl)
    {
        _page = page;
        _authBaseUrl = authBaseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_authBaseUrl}/privacy");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.GetByTestId("privacy-heading").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<string> GetContentAsync()
    {
        return await _page.GetByTestId("privacy-content").InnerTextAsync();
    }

    public async Task ClickBackAsync()
    {
        await _page.GetByTestId("privacy-back-button").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
