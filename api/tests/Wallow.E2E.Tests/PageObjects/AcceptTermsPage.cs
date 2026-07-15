using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class AcceptTermsPage
{
    private readonly IPage _page;
    private readonly string _authBaseUrl;

    public AcceptTermsPage(IPage page, string authBaseUrl)
    {
        _page = page;
        _authBaseUrl = authBaseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_authBaseUrl}/accept-terms");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.GetByTestId("accept-terms-heading").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task CheckTermsAsync()
    {
        await _page.GetByTestId("accept-terms-checkbox").ClickAsync();
    }

    public async Task CheckPrivacyAsync()
    {
        await _page.GetByTestId("accept-terms-privacy-checkbox").ClickAsync();
    }

    public async Task<bool> IsSubmitEnabledAsync()
    {
        return await _page.GetByTestId("accept-terms-submit").IsEnabledAsync();
    }

    public async Task<string> GetErrorTextAsync()
    {
        return await _page.GetByTestId("accept-terms-error").InnerTextAsync();
    }
}
