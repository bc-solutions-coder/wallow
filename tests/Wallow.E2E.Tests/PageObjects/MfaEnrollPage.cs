using Microsoft.Playwright;

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
        await AppRegistrationPage.WaitForBlazorCircuitAsync(_page);
        await _page.Locator("[data-testid='mfa-enroll-begin-setup']")
            .WaitForAsync(new() { Timeout = 10_000 });
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.Locator("[data-testid='mfa-enroll-begin-setup']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task ClickBeginSetupAsync()
    {
        await _page.Locator("[data-testid='mfa-enroll-begin-setup']").ClickAsync();
        await _page.Locator("[data-testid='mfa-enroll-secret']")
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    public async Task<bool> GetQrCodeAsync()
    {
        ILocator secret = _page.Locator("[data-testid='mfa-enroll-secret']");
        return await secret.IsVisibleAsync();
    }

    public async Task FillCodeAsync(string code)
    {
        await _page.Locator("[data-testid='mfa-enroll-code']").FillAsync(code);
    }

    public async Task SubmitAsync()
    {
        await _page.Locator("[data-testid='mfa-enroll-submit']").ClickAsync();
        await _page.Locator("[data-testid='mfa-enroll-error']")
            .WaitForAsync(new() { Timeout = 15_000 });
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
