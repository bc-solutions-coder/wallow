using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class AppRegistrationPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public AppRegistrationPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/dashboard/apps/register");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsLoadedAsync()
    {
        ILocator heading = _page.Locator("h1:has-text('Register New App')");
        return await heading.IsVisibleAsync();
    }

    public async Task FillFormAsync(
        string displayName,
        string clientType = "public",
        string? redirectUris = null,
        string? brandingDisplayName = null,
        string? brandingTagline = null)
    {
        await _page.Locator("input").Filter(new() { HasText = "" }).First.FillAsync(displayName);

        await _page.Locator("select").First.SelectOptionAsync(clientType);

        if (redirectUris is not null)
        {
            await _page.Locator("textarea").FillAsync(redirectUris);
        }

        if (brandingDisplayName is not null)
        {
            ILocator brandingNameInput = _page.Locator("input[placeholder='Your Company Name']");
            await brandingNameInput.FillAsync(brandingDisplayName);
        }

        if (brandingTagline is not null)
        {
            ILocator taglineInput = _page.Locator("input[placeholder='Your company tagline']");
            await taglineInput.FillAsync(brandingTagline);
        }
    }

    public async Task SubmitAsync()
    {
        await _page.Locator("button[type='submit']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<AppRegistrationResult> GetResultAsync()
    {
        ILocator successHeading = _page.Locator("h2:has-text('App Registered Successfully')");
        bool isSuccess = await successHeading.IsVisibleAsync();

        if (!isSuccess)
        {
            ILocator errorContainer = _page.Locator(".bg-red-50 p");
            bool hasError = await errorContainer.IsVisibleAsync();
            string? errorMessage = hasError ? await errorContainer.InnerTextAsync() : null;

            return new AppRegistrationResult(false, null, null, errorMessage);
        }

        ILocator clientIdCode = _page.Locator("code").First;
        string clientId = await clientIdCode.InnerTextAsync();

        ILocator secretCode = _page.Locator(".bg-amber-50 code");
        bool hasSecret = await secretCode.IsVisibleAsync();
        string? clientSecret = hasSecret ? await secretCode.InnerTextAsync() : null;

        return new AppRegistrationResult(true, clientId.Trim(), clientSecret?.Trim(), null);
    }

    public async Task ClickBackToAppsAsync()
    {
        await _page.Locator("a:has-text('Back to Apps')").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

}

public sealed record AppRegistrationResult(
    bool Success,
    string? ClientId,
    string? ClientSecret,
    string? ErrorMessage);
