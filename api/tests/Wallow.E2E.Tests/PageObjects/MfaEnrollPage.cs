using Microsoft.Playwright;
using Wallow.E2E.Tests.Infrastructure;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class MfaEnrollPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public MfaEnrollPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync(string? returnUrl = null)
    {
        string url = returnUrl is not null
            ? $"{_baseUrl}/mfa/enroll?returnUrl={Uri.EscapeDataString(returnUrl)}"
            : $"{_baseUrl}/mfa/enroll";

        await _page.GotoAsync(url);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await E2ETestBase.WaitForBlazorReadyAsync(_page);
        // Enrollment auto-starts; wait for either the secret or the fallback button.
        ILocator secret = _page.Locator("[data-testid='mfa-enroll-secret']");
        ILocator beginSetup = _page.Locator("[data-testid='mfa-enroll-begin-setup']");
        await secret.Or(beginSetup).WaitForAsync(new() { Timeout = 15_000 });
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            // When redirected from another app (e.g. Web settings), wait for
            // the URL to land on /mfa/enroll before checking for Blazor elements.
            await _page.WaitForURLAsync("**/mfa/enroll**", new() { Timeout = 15_000 });
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await E2ETestBase.WaitForBlazorReadyAsync(_page);
            // Enrollment auto-starts on page load. Wait for either the secret
            // (success) or the begin-setup fallback button (auth cookie missing).
            ILocator secret = _page.Locator("[data-testid='mfa-enroll-secret']");
            ILocator beginSetup = _page.Locator("[data-testid='mfa-enroll-begin-setup']");
            await secret.Or(beginSetup).WaitForAsync(new() { Timeout = 15_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task ClickBeginSetupAsync()
    {
        // Enrollment auto-starts on page load during prerender. If the secret
        // is already visible, the begin-setup button was never rendered.
        ILocator secret = _page.Locator("[data-testid='mfa-enroll-secret']");
        if (await secret.IsVisibleAsync())
        {
            return;
        }

        await _page.Locator("[data-testid='mfa-enroll-begin-setup']").ClickAsync();
        await secret.WaitForAsync(new() { Timeout = 15_000 });
    }

    public async Task<string> GetSecretTextAsync()
    {
        return await _page.Locator("[data-testid='mfa-enroll-secret']").InnerTextAsync();
    }

    public async Task<bool> GetQrCodeAsync()
    {
        ILocator qrCode = _page.Locator("[data-testid='mfa-enroll-qr']");
        return await qrCode.IsVisibleAsync();
    }

    public async Task FillCodeAsync(string code)
    {
        await _page.Locator("[data-testid='mfa-enroll-code']").FillAsync(code);
    }

    public async Task SubmitAsync(bool throwOnError = true)
    {
        // Before clicking, wait for any stale error from a previous attempt to
        // be cleared by Blazor's re-render. Without this, the race below can
        // immediately resolve on the still-visible error from the prior submit.
        ILocator errorLocator = _page.Locator("[data-testid='mfa-enroll-error']");
        try
        {
            await errorLocator.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 2_000 });
        }
        catch (TimeoutException)
        {
            // No stale error present or it didn't clear — safe to proceed.
        }

        await _page.Locator("[data-testid='mfa-enroll-submit']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Race both outcomes — whichever appears first wins.
        // Use a generous timeout: on cold databases the API call to confirm
        // enrollment and generate backup codes can be slow, and the Blazor
        // SignalR circuit must then push the DOM diff to the browser.
        ILocator backupCodesLocator = _page.Locator("[data-testid='mfa-enroll-backup-codes']");
        ILocator either = backupCodesLocator.Or(errorLocator);

        await either.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 45_000 });

        if (await errorLocator.IsVisibleAsync() && throwOnError)
        {
            string message = await errorLocator.InnerTextAsync();
            throw new InvalidOperationException($"MFA enrollment failed: {message}");
        }
    }

    public async Task CancelAsync()
    {
        ILocator cancelLink = _page.Locator("[data-testid='mfa-enroll-cancel']");
        await cancelLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        await cancelLink.ClickAsync();
        await _page.WaitForURLAsync(url => !url.Contains("/mfa/enroll"), new() { Timeout = 15_000 });
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task WaitForBackupCodesAsync()
    {
        await _page.Locator("[data-testid='mfa-enroll-backup-codes']")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });
    }

    public async Task<string?> GetErrorMessageAsync()
    {
        ILocator error = _page.Locator("[data-testid='mfa-enroll-error']");
        bool isVisible = await error.IsVisibleAsync();
        if (!isVisible)
        {
            return null;
        }

        return await error.InnerTextAsync();
    }

    public async Task<IReadOnlyList<string>> GetBackupCodesAsync()
    {
        ILocator backupCodes = _page.Locator("[data-testid='mfa-enroll-backup-codes']");
        string text = await backupCodes.InnerTextAsync();

        List<string> codes = [];
        foreach (string line in text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                codes.Add(trimmed);
            }
        }

        return codes;
    }
}
