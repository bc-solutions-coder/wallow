using Microsoft.Playwright;
using Wallow.E2E.Tests.Infrastructure;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class DashboardPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public DashboardPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/dashboard/apps");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.Locator("[data-testid='apps-heading']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<string?> GetWelcomeMessageAsync()
    {
        ILocator heading = _page.Locator("[data-testid='apps-heading']");
        bool isVisible = await heading.IsVisibleAsync();
        if (!isVisible)
        {
            return null;
        }

        return await heading.InnerTextAsync();
    }

    public async Task ClickLogoutAsync()
    {
        // No WaitForLoadState here — OIDC logout triggers a multi-hop cross-origin
        // redirect chain that never reaches NetworkIdle. Callers synchronize via WaitForURLAsync.
        await _page.Locator("[data-testid='dashboard-logout-link']").ClickAsync();
    }

    public async Task NavigateToAppsAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/dashboard/apps");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await E2ETestBase.WaitForBlazorReadyAsync(_page);
        await _page.Locator("[data-testid='apps-heading']").WaitForAsync(new() { Timeout = 10_000 });
    }

    public async Task<IReadOnlyList<AppRow>> GetAppRowsAsync()
    {
        IReadOnlyList<ILocator> rows = await _page.Locator("[data-testid='apps-row']").AllAsync();
        List<AppRow> results = new(rows.Count);

        foreach (ILocator row in rows)
        {
            string displayName = await row.Locator("[data-testid='apps-row-name']").InnerTextAsync();
            string clientId = await row.Locator("[data-testid='apps-row-client-id']").InnerTextAsync();
            string type = await row.Locator("[data-testid='apps-row-type']").InnerTextAsync();
            string createdAt = await row.Locator("[data-testid='apps-row-created']").InnerTextAsync();
            results.Add(new AppRow(displayName.Trim(), clientId.Trim(), type.Trim(), createdAt.Trim()));
        }

        return results;
    }

    public async Task<AppRow?> FindAppByNameAsync(string name)
    {
        IReadOnlyList<AppRow> rows = await GetAppRowsAsync();
        return rows.FirstOrDefault(r => r.DisplayName == name);
    }

    public async Task<bool> IsAppListEmptyAsync()
    {
        return await _page.Locator("[data-testid='apps-empty-state']").IsVisibleAsync();
    }
}

public sealed record AppRow(string DisplayName, string ClientId, string Type, string CreatedAt);
