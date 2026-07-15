using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class VerifyEmailPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public VerifyEmailPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/verify-email");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.Locator("[data-testid='verify-email-heading']")
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.Locator("[data-testid='verify-email-heading']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<string> GetHeadingTextAsync()
    {
        return await _page.Locator("[data-testid='verify-email-heading']").InnerTextAsync();
    }

    public async Task<string> GetDescriptionTextAsync()
    {
        return await _page.Locator("[data-testid='verify-email-description']").InnerTextAsync();
    }

    public async Task ClickBackToSignInAsync()
    {
        await _page.Locator("[data-testid='verify-email-back-link']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
