using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class LoginPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public LoginPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync(string? returnUrl = null)
    {
        string url = returnUrl is not null
            ? $"{_baseUrl}/login?returnUrl={Uri.EscapeDataString(returnUrl)}"
            : $"{_baseUrl}/login";

        await _page.GotoAsync(url);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.Locator("[data-testid='login-email']")
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    public async Task FillEmailAsync(string email)
    {
        await _page.Locator("[data-testid='login-email']").FillAsync(email);
    }

    public async Task FillPasswordAsync(string password)
    {
        await _page.Locator("[data-testid='login-password']").FillAsync(password);
    }

    public async Task CheckRememberMeAsync()
    {
        await _page.Locator("[data-testid='login-remember-me']").ClickAsync();
    }

    public async Task SubmitAsync()
    {
        await _page.Locator("[data-testid='login-submit']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<string?> GetErrorMessageAsync(int timeoutMs = 3_000)
    {
        ILocator error = _page.Locator("[data-testid='login-error']");
        try
        {
            await error.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
            return await error.InnerTextAsync();
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    public async Task<bool> IsErrorVisibleAsync(int timeoutMs = 3_000)
    {
        ILocator error = _page.Locator("[data-testid='login-error']");
        try
        {
            await error.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.Locator("[data-testid='login-email']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task ClickForgotPasswordAsync()
    {
        await _page.Locator("[data-testid='login-forgot-password']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task ClickRegisterLinkAsync()
    {
        await _page.Locator("[data-testid='login-register-link']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    // Magic link tab methods

    public async Task SwitchToMagicLinkTabAsync()
    {
        await _page.Locator("[data-testid='login-tab-magic-link']").ClickAsync();
        await _page.Locator("[data-testid='login-magic-link-email']")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });
    }

    public async Task FillMagicLinkEmailAsync(string email)
    {
        await _page.Locator("[data-testid='login-magic-link-email']").FillAsync(email);
    }

    public async Task SubmitMagicLinkAsync()
    {
        await _page.Locator("[data-testid='login-magic-link-submit']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsMagicLinkSentVisibleAsync(int timeoutMs = 5_000)
    {
        ILocator sent = _page.Locator("[data-testid='login-magic-link-sent']");
        try
        {
            await sent.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    // OTP tab methods

    public async Task SwitchToOtpTabAsync()
    {
        await _page.Locator("[data-testid='login-tab-otp']").ClickAsync();
        await _page.Locator("[data-testid='login-otp-email']")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5_000 });
    }

    public async Task FillOtpEmailAsync(string email)
    {
        await _page.Locator("[data-testid='login-otp-email']").FillAsync(email);
    }

    public async Task SubmitOtpRequestAsync()
    {
        await _page.Locator("[data-testid='login-otp-send-submit']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsOtpCodeFormVisibleAsync(int timeoutMs = 5_000)
    {
        ILocator codeField = _page.Locator("[data-testid='login-otp-code'], [data-testid='login-otp-sent']");
        try
        {
            await codeField.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task FillOtpCodeAsync(string code)
    {
        await _page.Locator("[data-testid='login-otp-code']").FillAsync(code);
    }

    public async Task SubmitOtpVerifyAsync()
    {
        await _page.Locator("[data-testid='login-otp-verify-submit']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
