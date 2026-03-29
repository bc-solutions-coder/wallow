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

    public async Task<string?> GetErrorMessageAsync()
    {
        ILocator error = _page.Locator("[data-testid='login-error']");
        bool isVisible = await error.IsVisibleAsync();
        if (!isVisible)
        {
            return null;
        }

        return await error.InnerTextAsync();
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
}
