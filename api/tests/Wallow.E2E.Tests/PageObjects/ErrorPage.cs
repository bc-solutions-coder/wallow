using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class ErrorPage
{
    private readonly IPage _page;
    private readonly string _authBaseUrl;

    public ErrorPage(IPage page, string authBaseUrl)
    {
        _page = page;
        _authBaseUrl = authBaseUrl;
    }

    public async Task NavigateAsync(string? reason = null)
    {
        string url = reason is not null
            ? $"{_authBaseUrl}/error?reason={Uri.EscapeDataString(reason)}"
            : $"{_authBaseUrl}/error";

        await _page.GotoAsync(url);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.GetByTestId("error-heading").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<string> GetErrorMessageAsync()
    {
        return await _page.GetByTestId("error-message").InnerTextAsync();
    }

    public async Task<bool> IsSignOutLinkVisibleAsync()
    {
        return await _page.GetByTestId("error-sign-out-link").IsVisibleAsync();
    }
}
