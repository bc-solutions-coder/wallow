using Microsoft.Playwright;
using Wallow.E2E.Tests.Infrastructure;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class InvitationLandingPage
{
    private readonly IPage _page;
    private readonly string _authBaseUrl;

    public InvitationLandingPage(IPage page, string authBaseUrl)
    {
        _page = page;
        _authBaseUrl = authBaseUrl;
    }

    public async Task NavigateAsync(string token)
    {
        await _page.GotoAsync($"{_authBaseUrl}/invitation?token={Uri.EscapeDataString(token)}");
        await E2ETestBase.WaitForBlazorReadyAsync(_page);
    }

    public async Task<bool> IsLoadedAsync()
    {
        // Page is loaded when the loading spinner/skeleton is no longer visible
        return !await _page.Locator("[data-testid='invitation-loading']").IsVisibleAsync();
    }

    public async Task<bool> IsErrorVisibleAsync()
    {
        return await _page.Locator("[data-testid='invitation-error']").IsVisibleAsync();
    }

    public async Task<string> GetErrorTextAsync()
    {
        return await _page.Locator("[data-testid='invitation-error']").InnerTextAsync();
    }

    public async Task<bool> IsExpiredAsync()
    {
        return await _page.Locator("[data-testid='invitation-expired']").IsVisibleAsync();
    }

    public async Task<bool> IsAcceptFormVisibleAsync()
    {
        return await _page.Locator("[data-testid='invitation-accept']").IsVisibleAsync();
    }

    public async Task ClickAcceptAsync()
    {
        await _page.Locator("[data-testid='invitation-accept']").ClickAsync();

        // Wait for navigation to complete or an inline error to appear
        await _page.WaitForFunctionAsync(
            "() => document.readyState === 'complete'",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });

        try
        {
            await _page.Locator("[data-testid='invitation-accept-error']")
                .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 2_000 });
        }
        catch (TimeoutException)
        {
            // No inline error appeared — navigation completed successfully
        }
    }

    public async Task ClickDeclineAsync()
    {
        await _page.Locator("[data-testid='invitation-decline']").ClickAsync();
    }

    public async Task<bool> IsSignInPromptVisibleAsync()
    {
        return await _page.Locator("[data-testid='invitation-sign-in']").IsVisibleAsync();
    }

    public async Task<string?> GetSignInHrefAsync()
    {
        return await _page.Locator("[data-testid='invitation-sign-in']").GetAttributeAsync("href");
    }

    public async Task<string?> GetCreateAccountHrefAsync()
    {
        return await _page.Locator("[data-testid='invitation-create-account']").GetAttributeAsync("href");
    }
}
