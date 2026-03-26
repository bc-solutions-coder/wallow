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
    }

    public async Task FillFormAsync(string email, string password, string confirmPassword, bool acceptTerms = true, bool acceptPrivacy = true)
    {
        await _page.Locator("#email").FillAsync(email);
        await _page.Locator("#password").FillAsync(password);
        await _page.Locator("#confirmPassword").FillAsync(confirmPassword);

        if (acceptTerms)
        {
            await _page.Locator("#termsAccepted").ClickAsync();
        }

        if (acceptPrivacy)
        {
            await _page.Locator("#privacyAccepted").ClickAsync();
        }
    }

    public async Task SubmitAsync()
    {
        await _page.Locator("button[type='submit']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<IReadOnlyList<string>> GetValidationErrorsAsync()
    {
        // Error messages appear in danger alerts and inline validation text
        ILocator alerts = _page.Locator("[data-variant='danger']");
        int alertCount = await alerts.CountAsync();

        List<string> errors = [];
        for (int i = 0; i < alertCount; i++)
        {
            string text = await alerts.Nth(i).InnerTextAsync();
            errors.Add(text.Trim());
        }

        ILocator inlineErrors = _page.Locator(".text-destructive");
        int inlineCount = await inlineErrors.CountAsync();

        for (int i = 0; i < inlineCount; i++)
        {
            string text = await inlineErrors.Nth(i).InnerTextAsync();
            errors.Add(text.Trim());
        }

        return errors;
    }

    public async Task<bool> IsLoadedAsync()
    {
        ILocator title = _page.Locator("text=Create an account");
        return await title.IsVisibleAsync();
    }
}
