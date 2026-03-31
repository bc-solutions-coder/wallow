using Microsoft.Playwright;
using Wallow.E2E.Tests.Infrastructure;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class VerifyEmailConfirmPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public VerifyEmailConfirmPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync(string token, string email, string? returnUrl = null)
    {
        List<string> queryParams =
        [
            $"token={Uri.EscapeDataString(token)}",
            $"email={Uri.EscapeDataString(email)}",
        ];

        if (returnUrl is not null)
        {
            queryParams.Add($"returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        string url = $"{_baseUrl}/verify-email/confirm?{string.Join("&", queryParams)}";

        await _page.GotoAsync(url);
        await E2ETestBase.WaitForBlazorReadyAsync(_page);
    }

    public async Task NavigateToLinkAsync(string verificationLink)
    {
        await _page.GotoAsync(verificationLink);
        await E2ETestBase.WaitForBlazorReadyAsync(_page);
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.Locator("[data-testid='verify-email-confirm-success'], [data-testid='verify-email-confirm-error']")
                .First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task WaitForSuccessAsync(int timeoutMs = 15_000)
    {
        await _page.Locator("[data-testid='verify-email-confirm-success']")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
    }

    public async Task WaitForErrorAsync(int timeoutMs = 15_000)
    {
        await _page.Locator("[data-testid='verify-email-confirm-error']")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
    }

    public async Task<string> GetSuccessTextAsync()
    {
        return await _page.Locator("[data-testid='verify-email-confirm-success']").InnerTextAsync();
    }

    public async Task<string> GetErrorTextAsync()
    {
        return await _page.Locator("[data-testid='verify-email-confirm-error']").InnerTextAsync();
    }

    public async Task<bool> IsLoadingVisibleAsync()
    {
        return await _page.Locator("[data-testid='verify-email-confirm-loading']").IsVisibleAsync();
    }

    public async Task ClickContinueAsync()
    {
        await _page.Locator("[data-testid='verify-email-confirm-continue']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task ClickSignInLinkAsync()
    {
        await _page.Locator("[data-testid='verify-email-confirm-signin-link']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
