using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class OrganizationPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public OrganizationPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/dashboard/organizations");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await AppRegistrationPage.WaitForBlazorCircuitAsync(_page);
        await _page.Locator("[data-testid='organizations-heading']")
            .WaitForAsync(new() { Timeout = 10_000 });
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.Locator("[data-testid='organizations-heading']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<OrganizationRow>> GetOrganizationsAsync()
    {
        ILocator rows = _page.Locator("[data-testid='organizations-row']");
        int count = await rows.CountAsync();

        List<OrganizationRow> organizations = [];
        for (int i = 0; i < count; i++)
        {
            ILocator row = rows.Nth(i);
            ILocator cells = row.Locator("td");

            string name = await cells.Nth(0).InnerTextAsync();
            string domain = await cells.Nth(1).InnerTextAsync();
            string memberCount = await cells.Nth(2).InnerTextAsync();

            organizations.Add(new OrganizationRow(name.Trim(), domain.Trim(), memberCount.Trim()));
        }

        return organizations;
    }

    public async Task<bool> IsEmptyStateAsync()
    {
        ILocator emptyState = _page.Locator("[data-testid='organizations-empty-state']");
        return await emptyState.IsVisibleAsync();
    }

    public async Task ClickCreateOrganizationAsync()
    {
        await _page.Locator("[data-testid='organizations-create-link']").ClickAsync();
    }
}

public sealed record OrganizationRow(string Name, string Domain, string MemberCount);
