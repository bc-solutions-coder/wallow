using Microsoft.Playwright;
using Wallow.E2E.Tests.Infrastructure;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class SettingsMfaSection
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public SettingsMfaSection(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/dashboard/settings");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await E2ETestBase.WaitForBlazorReadyAsync(_page);
        await _page.GetByTestId("settings-mfa-status").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 10_000 });
    }

    public async Task<string> GetMfaStatusAsync()
    {
        return await _page.GetByTestId("settings-mfa-status").InnerTextAsync();
    }

    public async Task<int> GetBackupCodeCountAsync()
    {
        ILocator backupCount = _page.GetByTestId("settings-mfa-backup-count");
        await backupCount.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        string text = await backupCount.InnerTextAsync();
        return int.Parse(text.Trim());
    }

    public async Task ClickEnableAsync()
    {
        await _page.GetByTestId("settings-mfa-enable").ClickAsync();
    }

    public async Task ClickDisableAsync()
    {
        await _page.GetByTestId("settings-mfa-disable").ClickAsync();
        // Wait for Blazor to re-render and show the confirmation dialog
        await _page.GetByTestId("settings-mfa-confirm-password").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 10_000 });
    }

    public async Task ClickRegenerateCodesAsync()
    {
        await _page.GetByTestId("settings-mfa-regenerate").ClickAsync();
        // Wait for Blazor to re-render and show the confirmation dialog
        await _page.GetByTestId("settings-mfa-confirm-password").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 10_000 });
    }

    public async Task ConfirmPasswordAsync(string password)
    {
        ILocator confirmSubmit = _page.GetByTestId("settings-mfa-confirm-submit");
        await _page.GetByTestId("settings-mfa-confirm-password").FillAsync(password);
        await confirmSubmit.ClickAsync();

        // Wait for the confirm dialog to disappear — this signals the API call completed
        // and Blazor re-rendered (both disable and regenerate flows hide the confirm dialog on success)
        await Assertions.Expect(confirmSubmit).ToBeHiddenAsync(new() { Timeout = 15_000 });

        // Allow Blazor to finish any remaining re-render after the status refresh API call
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await E2ETestBase.WaitForBlazorReadyAsync(_page);

        await _page.GetByTestId("settings-mfa-status").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 10_000 });
    }

    public async Task<string?> GetErrorMessageAsync()
    {
        ILocator error = _page.GetByTestId("settings-mfa-error");
        bool isVisible = await error.IsVisibleAsync();
        if (!isVisible)
        {
            return null;
        }

        return await error.InnerTextAsync();
    }

    public async Task<string> SubmitPasswordAndExpectErrorAsync(string password)
    {
        await _page.GetByTestId("settings-mfa-confirm-password").FillAsync(password);
        await _page.GetByTestId("settings-mfa-confirm-submit").ClickAsync();

        ILocator error = _page.GetByTestId("settings-mfa-error");
        await error.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 15_000
        });

        return await error.InnerTextAsync();
    }

    public async Task WaitForMfaStatusAsync(string expectedText, int timeoutMs = 10_000)
    {
        await Assertions.Expect(_page.GetByTestId("settings-mfa-status"))
            .ToContainTextAsync(expectedText, new() { Timeout = timeoutMs });
    }
}
