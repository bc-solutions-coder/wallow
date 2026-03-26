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
    }

    public async Task<bool> IsLoadedAsync()
    {
        ILocator heading = _page.Locator("h1:has-text('Organizations')");
        return await heading.IsVisibleAsync();
    }

    public async Task<IReadOnlyList<OrganizationRow>> GetOrganizationsAsync()
    {
        ILocator rows = _page.Locator("tbody tr");
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
        ILocator emptyMessage = _page.Locator("text=No organizations yet");
        return await emptyMessage.IsVisibleAsync();
    }

    public async Task ClickCreateOrganizationAsync()
    {
        await _page.Locator("a:has-text('Create Organization')").First.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task ViewDetailAsync(string organizationName)
    {
        await _page.Locator($"td:has-text('{organizationName}')").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

}

public sealed record OrganizationRow(string Name, string Domain, string MemberCount);
