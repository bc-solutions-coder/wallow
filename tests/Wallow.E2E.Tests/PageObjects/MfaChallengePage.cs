using Microsoft.Playwright;
using Wallow.E2E.Tests.Infrastructure;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class MfaChallengePage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public MfaChallengePage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync(string? returnUrl = null)
    {
        string url = returnUrl is not null
            ? $"{_baseUrl}/mfa/challenge?returnUrl={Uri.EscapeDataString(returnUrl)}"
            : $"{_baseUrl}/mfa/challenge";

        await _page.GotoAsync(url);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await E2ETestBase.WaitForBlazorReadyAsync(_page);
        await _page.Locator("[data-testid='mfa-challenge-code']")
            .WaitForAsync(new() { Timeout = 10_000 });
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await E2ETestBase.WaitForBlazorReadyAsync(_page);
            await _page.Locator("[data-testid='mfa-challenge-code']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task FillCodeAsync(string code)
    {
        await _page.Locator("[data-testid='mfa-challenge-code']").FillAsync(code);
    }

    public async Task SubmitAsync()
    {
        await _page.Locator("[data-testid='mfa-challenge-submit']").ClickAsync();

        // After submit, either:
        // 1. The success element appears (no returnUrl / direct login)
        // 2. An error element appears
        // 3. The page redirects away from /mfa/challenge (OIDC flow with returnUrl)
        ILocator successLocator = _page.Locator("[data-testid='mfa-challenge-success']");
        ILocator errorLocator = _page.Locator("[data-testid='mfa-challenge-error']");
        ILocator either = successLocator.Or(errorLocator);

        try
        {
            await either.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        }
        catch (TimeoutException)
        {
            // The page may have redirected away (OIDC flow). Check if URL changed.
            if (!_page.Url.Contains("/mfa/challenge", StringComparison.OrdinalIgnoreCase))
            {
                return; // Redirect happened — success via navigation
            }
            // Otherwise allow caller to inspect state via IsSuccessAsync/GetErrorAsync
        }
    }

    public async Task ToggleBackupCodeAsync()
    {
        await _page.Locator("[data-testid='mfa-challenge-toggle-backup']").ClickAsync();
        // Wait for Blazor to re-render and swap the TOTP input for the backup code input
        await _page.Locator("[data-testid='mfa-challenge-backup-code']")
            .WaitForAsync(new() { Timeout = 10_000 });
    }

    public async Task FillBackupCodeAsync(string code)
    {
        await _page.Locator("[data-testid='mfa-challenge-backup-code']").FillAsync(code);
    }

    public async Task<string?> GetErrorAsync()
    {
        ILocator error = _page.Locator("[data-testid='mfa-challenge-error']");
        bool isVisible = await error.IsVisibleAsync();
        if (!isVisible)
        {
            return null;
        }

        return await error.InnerTextAsync();
    }

    public async Task<bool> IsSuccessAsync(int timeoutMs = 10_000)
    {
        // Success means either the success element is visible (direct login)
        // or the page redirected away from /mfa/challenge (OIDC flow)
        if (!_page.Url.Contains("/mfa/challenge", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            await _page.GetByTestId("mfa-challenge-success")
                .WaitForAsync(new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            // Check again — redirect may have happened while waiting
            return !_page.Url.Contains("/mfa/challenge", StringComparison.OrdinalIgnoreCase);
        }
    }
}
