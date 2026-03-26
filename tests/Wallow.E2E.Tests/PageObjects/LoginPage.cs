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
    }

    public async Task FillEmailAsync(string email)
    {
        await _page.Locator("#email").FillAsync(email);
    }

    public async Task FillPasswordAsync(string password)
    {
        await _page.Locator("#password").FillAsync(password);
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

    public async Task<bool> IsLoadedAsync()
    {
        ILocator title = _page.Locator("text=Sign in to your account");
        return await title.IsVisibleAsync();
    }

    public async Task ClickForgotPasswordAsync()
    {
        await _page.Locator("a[href='/forgot-password']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task ClickRegisterLinkAsync()
    {
        await _page.Locator("a:has-text('Register')").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
