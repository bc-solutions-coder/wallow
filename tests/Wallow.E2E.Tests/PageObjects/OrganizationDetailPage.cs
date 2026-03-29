using Microsoft.Playwright;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class OrganizationDetailPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public OrganizationDetailPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync(Guid orgId)
    {
        await _page.GotoAsync($"{_baseUrl}/dashboard/organizations/{orgId}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await AppRegistrationPage.WaitForBlazorCircuitAsync(_page);
        await _page.Locator("[data-testid='organization-detail-heading']")
            .WaitForAsync(new() { Timeout = 10_000 });
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.Locator("[data-testid='organization-detail-heading']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<bool> IsNotFoundAsync()
    {
        ILocator notFound = _page.Locator("[data-testid='organization-detail-not-found']");
        return await notFound.IsVisibleAsync();
    }

    public async Task<IReadOnlyList<(string Email, string Role)>> GetMemberRowsAsync()
    {
        ILocator rows = _page.Locator("[data-testid='organization-detail-member-row']");
        int count = await rows.CountAsync();

        List<(string Email, string Role)> members = [];
        for (int i = 0; i < count; i++)
        {
            ILocator row = rows.Nth(i);
            ILocator cells = row.Locator("td");

            string email = await cells.Nth(0).InnerTextAsync();
            string role = await cells.Nth(1).InnerTextAsync();

            members.Add((email.Trim(), role.Trim()));
        }

        return members;
    }

    public async Task<IReadOnlyList<(string Name, string Type)>> GetClientRowsAsync()
    {
        ILocator rows = _page.Locator("[data-testid='organization-detail-client-row']");
        int count = await rows.CountAsync();

        List<(string Name, string Type)> clients = [];
        for (int i = 0; i < count; i++)
        {
            ILocator row = rows.Nth(i);
            ILocator cells = row.Locator("td");

            string name = await cells.Nth(0).InnerTextAsync();
            string type = await cells.Nth(1).InnerTextAsync();

            clients.Add((name.Trim(), type.Trim()));
        }

        return clients;
    }

    public async Task FillRegisterClientFormAsync(string name, string type, string? redirectUris)
    {
        await _page.Locator("[data-testid='organization-detail-register-name']").FillAsync(name);
        await _page.Locator("[data-testid='organization-detail-register-type']").SelectOptionAsync(type);

        if (redirectUris is not null)
        {
            await _page.Locator("[data-testid='organization-detail-register-redirect-uris']").FillAsync(redirectUris);
        }
    }

    public async Task SubmitRegisterClientAsync()
    {
        await _page.Locator("[data-testid='organization-detail-register-submit']").ClickAsync();
        // Wait for server response via SignalR (not HTTP)
        await _page.Locator("[data-testid='organization-detail-register-success'], [data-testid='organization-detail-register-error']")
            .First.WaitForAsync(new() { Timeout = 15_000 });
    }

    public async Task<(bool Success, string? ClientId, string? Error)> GetRegisterResultAsync()
    {
        ILocator success = _page.Locator("[data-testid='organization-detail-register-success']");
        bool isSuccess = await success.IsVisibleAsync();

        if (!isSuccess)
        {
            ILocator error = _page.Locator("[data-testid='organization-detail-register-error']");
            bool hasError = await error.IsVisibleAsync();
            string? errorMessage = hasError ? await error.InnerTextAsync() : null;

            return (false, null, errorMessage);
        }

        ILocator clientIdLocator = _page.Locator("[data-testid='organization-detail-register-client-id']");
        string clientId = await clientIdLocator.InnerTextAsync();

        return (true, clientId.Trim(), null);
    }
}
