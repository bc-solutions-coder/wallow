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
        await WaitForBlazorCircuitAsync(_page);
        await _page.Locator("[data-testid='register-app-display-name']")
            .WaitForAsync(new() { Timeout = 10_000 });
    }

    internal static async Task WaitForBlazorCircuitAsync(IPage page, int timeoutMs = 30_000)
    {
        // Wait for Blazor circuit to be fully connected.
        // Check for data-blazor-ready (set by BlazorReadyIndicator component)
        // or fall back to checking the Blazor global object.
        await page.WaitForFunctionAsync(
            "() => document.body.getAttribute('data-blazor-ready') === 'true' || (typeof Blazor !== 'undefined')",
            null,
            new() { Timeout = timeoutMs, PollingInterval = 200 });
        // Blazor circuit needs time after JS loads to establish SignalR connection
        // and replace SSR content with interactive components.
        await Task.Delay(2000);
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.Locator("[data-testid='register-app-display-name']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task FillFormAsync(
        string displayName,
        string clientType = "public",
        string? redirectUris = null)
    {
        await _page.Locator("[data-testid='register-app-display-name']").FillAsync(displayName);
        await _page.Locator("[data-testid='register-app-client-type']").SelectOptionAsync(clientType);

        if (redirectUris is not null)
        {
            await _page.Locator("[data-testid='register-app-redirect-uris']").FillAsync(redirectUris);
        }
    }

    public async Task SubmitAsync()
    {
        await _page.Locator("[data-testid='register-app-submit']").ClickAsync();
        // Wait for server response via SignalR (not HTTP)
        await _page.Locator("[data-testid='register-app-success'], [data-testid='register-app-error']")
            .First.WaitForAsync(new() { Timeout = 15_000 });
    }

    public async Task<AppRegistrationResult> GetResultAsync()
    {
        ILocator success = _page.Locator("[data-testid='register-app-success']");
        bool isSuccess = await success.IsVisibleAsync();

        if (!isSuccess)
        {
            ILocator error = _page.Locator("[data-testid='register-app-error']");
            bool hasError = await error.IsVisibleAsync();
            string? errorMessage = hasError ? await error.InnerTextAsync() : null;

            return new AppRegistrationResult(false, null, null, errorMessage);
        }

        ILocator clientIdLocator = _page.Locator("[data-testid='register-app-client-id']");
        string clientId = await clientIdLocator.InnerTextAsync();

        return new AppRegistrationResult(true, clientId.Trim(), null, null);
    }
}

public sealed record AppRegistrationResult(
    bool Success,
    string? ClientId,
    string? ClientSecret,
    string? ErrorMessage);
