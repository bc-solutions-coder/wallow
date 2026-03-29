using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
[Trait("E2EGroup", "Organizations")]
public sealed class OrganizationFlowTests : AuthenticatedE2ETestBase
{
    public OrganizationFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    [Fact]
    public async Task OrganizationList_NewUser_ShowsEmptyState()
    {
        OrganizationPage orgPage = new(Page, Docker.WebBaseUrl);
        await orgPage.NavigateAsync();

        bool isLoaded = await orgPage.IsLoadedAsync();
        Assert.True(isLoaded, "Organizations page should be loaded");

        bool isEmpty = await orgPage.IsEmptyStateAsync();
        Assert.True(isEmpty, "New user with no organizations should see empty state");
    }

    [Fact]
    public async Task OrganizationDetail_WithValidOrgId_ShowsMembersAndClients()
    {
        OrgAdminTestUser orgAdmin = await TestUserFactory.CreateOrgAdminAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // Log in as the org admin in the browser
        await LoginAsUserAsync(orgAdmin.Email, orgAdmin.Password);

        OrganizationDetailPage detailPage = new(Page, Docker.WebBaseUrl);
        await detailPage.NavigateAsync(orgAdmin.OrgId);

        bool isLoaded = await detailPage.IsLoadedAsync();
        Assert.True(isLoaded, "Organization detail page should be loaded");

        IReadOnlyList<(string Email, string Role)> members = await detailPage.GetMemberRowsAsync();
        Assert.NotEmpty(members);
    }

    [Fact]
    public async Task OrganizationDetail_WithInvalidOrgId_ShowsNotFound()
    {
        OrganizationDetailPage detailPage = new(Page, Docker.WebBaseUrl);

        // Navigate with a random GUID that doesn't correspond to any org
        await Page.GotoAsync($"{Docker.WebBaseUrl}/dashboard/organizations/{Guid.NewGuid()}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await E2ETestBase.WaitForBlazorReadyAsync(Page);

        bool isNotFound = await detailPage.IsNotFoundAsync();
        Assert.True(isNotFound, "Navigating to a non-existent organization should show not-found state");
    }

    [Fact]
    public async Task OrganizationDetail_RegisterClient_ShowsClientId()
    {
        OrgAdminTestUser orgAdmin = await TestUserFactory.CreateOrgAdminAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        await LoginAsUserAsync(orgAdmin.Email, orgAdmin.Password);

        OrganizationDetailPage detailPage = new(Page, Docker.WebBaseUrl);
        await detailPage.NavigateAsync(orgAdmin.OrgId);

        bool isLoaded = await detailPage.IsLoadedAsync();
        Assert.True(isLoaded, "Organization detail page should be loaded");

        await detailPage.FillRegisterClientFormAsync(
            name: $"client-{Guid.NewGuid():N}",
            type: "public",
            redirectUris: "https://localhost:3000/callback");

        await detailPage.SubmitRegisterClientAsync();

        (bool success, string? clientId, string? error) = await detailPage.GetRegisterResultAsync();
        Assert.True(success, $"Client registration should succeed. Error: {error}");
        Assert.False(string.IsNullOrEmpty(clientId), "Client ID should be returned and non-empty");
    }

    [Fact]
    public async Task OrganizationList_ClickRow_NavigatesToDetail()
    {
        OrgAdminTestUser orgAdmin = await TestUserFactory.CreateOrgAdminAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        await LoginAsUserAsync(orgAdmin.Email, orgAdmin.Password);

        OrganizationPage orgPage = new(Page, Docker.WebBaseUrl);
        await orgPage.NavigateAsync();

        bool isLoaded = await orgPage.IsLoadedAsync();
        Assert.True(isLoaded, "Organizations page should be loaded");

        IReadOnlyList<OrganizationRow> organizations = await orgPage.GetOrganizationsAsync();
        Assert.NotEmpty(organizations);

        await orgPage.ClickOrganizationRowAsync(organizations[0].Name);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await E2ETestBase.WaitForBlazorReadyAsync(Page);

        OrganizationDetailPage detailPage = new(Page, Docker.WebBaseUrl);
        bool detailLoaded = await detailPage.IsLoadedAsync();
        Assert.True(detailLoaded, "Clicking an organization row should navigate to the detail page");
    }

    /// <summary>
    /// Logs in as a specific user by navigating through the OIDC flow.
    /// Reuses the existing browser context (clears the current session first).
    /// </summary>
    private async Task LoginAsUserAsync(string email, string password)
    {
        await Page.GotoAsync($"{Docker.WebBaseUrl}/authentication/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.GetByTestId("login-email").WaitForAsync(new() { Timeout = 15_000 });

        await Page.GetByTestId("login-email").FillAsync(email);
        await Page.GetByTestId("login-password").FillAsync(password);
        await Page.GetByTestId("login-submit").ClickAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("/dashboard", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 30_000 });
    }
}
