using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class RegisterPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public RegisterPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync(string? clientId = null, string? returnUrl = null)
    {
        List<string> queryParams = [];

        if (clientId is not null)
        {
            queryParams.Add($"client_id={Uri.EscapeDataString(clientId)}");
        }

        if (returnUrl is not null)
        {
            queryParams.Add($"returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        string url = queryParams.Count > 0
            ? $"{_baseUrl}/register?{string.Join("&", queryParams)}"
            : $"{_baseUrl}/register";

        await _page.GotoAsync(url);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.Locator("[data-testid='register-email']")
            .WaitForAsync(new() { Timeout = 15_000 });
    }

    public async Task FillFormAsync(string email, string password, string confirmPassword, bool acceptTerms = true, bool acceptPrivacy = true)
    {
        await _page.Locator("[data-testid='register-email']").FillAsync(email);
        await _page.Locator("[data-testid='register-password']").FillAsync(password);
        await _page.Locator("[data-testid='register-confirm-password']").FillAsync(confirmPassword);

        if (acceptTerms)
        {
            await _page.Locator("[data-testid='register-terms']").ClickAsync();
        }

        if (acceptPrivacy)
        {
            await _page.Locator("[data-testid='register-privacy']").ClickAsync();
        }
    }

    public async Task SubmitAsync()
    {
        await _page.Locator("[data-testid='register-submit']").ClickAsync();
        // Blazor uses SignalR — caller handles navigation/error waits
    }

    public async Task<IReadOnlyList<string>> GetValidationErrorsAsync()
    {
        ILocator errors = _page.Locator("[data-testid='register-error']");
        int count = await errors.CountAsync();

        List<string> errorMessages = [];
        for (int i = 0; i < count; i++)
        {
            string text = await errors.Nth(i).InnerTextAsync();
            errorMessages.Add(text.Trim());
        }

        return errorMessages;
    }

    public async Task<string> GetErrorMessageAsync(int timeoutMs = 3_000)
    {
        ILocator error = _page.Locator("[data-testid='register-error']");
        try
        {
            await error.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
            return await error.InnerTextAsync();
        }
        catch (TimeoutException)
        {
            return string.Empty;
        }
    }

    public async Task<bool> IsErrorVisibleAsync(int timeoutMs = 3_000)
    {
        ILocator error = _page.Locator("[data-testid='register-error']");
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
            await _page.Locator("[data-testid='register-email']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
