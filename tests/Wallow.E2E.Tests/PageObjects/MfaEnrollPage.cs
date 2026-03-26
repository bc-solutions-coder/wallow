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
    }

    public async Task<bool> IsLoadedAsync()
    {
        ILocator title = _page.Locator("text=Set up two-factor authentication");
        return await title.IsVisibleAsync();
    }

    public async Task ClickBeginSetupAsync()
    {
        await _page.Locator("button:has-text('Begin setup')").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> GetQrCodeAsync()
    {
        ILocator secretDisplay = _page.Locator(".font-mono.text-sm");
        return await secretDisplay.IsVisibleAsync();
    }

    public async Task FillCodeAsync(string code)
    {
        await _page.Locator("#code").FillAsync(code);
    }

    public async Task SubmitAsync()
    {
        await _page.Locator("button[type='submit']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<string?> GetErrorMessageAsync()
    {
        ILocator alert = _page.Locator("[data-variant='danger']");
        bool isVisible = await alert.IsVisibleAsync();
        if (!isVisible)
        {
            return null;
        }

        return await alert.InnerTextAsync();
    }

    public async Task<bool> IsSuccessAsync()
    {
        ILocator successAlert = _page.Locator("text=MFA enabled successfully");
        return await successAlert.IsVisibleAsync();
    }

    public async Task<IReadOnlyList<string>> GetBackupCodesAsync()
    {
        ILocator codeElements = _page.Locator(".bg-muted.rounded-md.p-4.font-mono div");
        int count = await codeElements.CountAsync();

        List<string> codes = [];
        for (int i = 0; i < count; i++)
        {
            string text = await codeElements.Nth(i).InnerTextAsync();
            codes.Add(text.Trim());
        }

        return codes;
    }

    public async Task ClickDoneAsync()
    {
        await _page.Locator("button:has-text('Done')").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task ClickCancelAsync()
    {
        await _page.Locator("a:has-text('Cancel')").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
