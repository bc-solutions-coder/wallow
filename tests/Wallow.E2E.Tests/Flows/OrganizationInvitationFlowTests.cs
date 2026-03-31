using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
[Trait("E2EGroup", "Organizations")]
[Trait("E2EGroup", "Invitations")]
public sealed class OrganizationInvitationFlowTests : AuthenticatedE2ETestBase
{
    public OrganizationInvitationFlowTests(
        DockerComposeFixture docker,
        PlaywrightFixture playwright,
        ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    [Fact(Skip = "Pending fix - tracked in beads")]
    public async Task AuthenticatedUser_AcceptsInvitation_JoinsOrganization()
    {
        // Create an org admin who will send the invitation
        OrgAdminTestUser orgAdmin = await TestUserFactory.CreateOrgAdminAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // The base class already created and logged in TestUser — use them as the invitee
        string inviteeEmail = TestUser.Email;

        // Create an invitation for the invitee via the API
        await InvitationHelper.CreateInvitationAsync(
            Docker.ApiBaseUrl, orgAdmin.AuthCookie, inviteeEmail);

        // Retrieve the invitation link from the invitee's email
        string invitationLink = await InvitationHelper.SearchForInvitationLinkAsync(
            Docker.MailpitBaseUrl, inviteeEmail);
        Assert.False(string.IsNullOrEmpty(invitationLink), "Invitation email should contain a link");

        // Extract the token from the invitation link
        Uri uri = new(invitationLink);
        string? token = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("token");
        Assert.False(string.IsNullOrEmpty(token), "Invitation link should contain a token parameter");

        // Navigate to the invitation landing page (user is already authenticated)
        InvitationLandingPage landingPage = new(Page, Docker.AuthBaseUrl);
        await landingPage.NavigateAsync(token!);

        bool isLoaded = await landingPage.IsLoadedAsync();
        Assert.True(isLoaded, "Invitation landing page should be loaded");

        // Accept the invitation
        await landingPage.ClickAcceptAsync();

        // After accepting, the user should be navigated away from the invitation page
        await Page.WaitForURLAsync(
            url => !url.Contains("/invitation", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 15_000 });

        Assert.DoesNotContain("/invitation", Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "Pending fix - tracked in beads")]
    public async Task AuthenticatedUser_DeclinesInvitation_StaysUnenrolled()
    {
        // Create an org admin who will send the invitation
        OrgAdminTestUser orgAdmin = await TestUserFactory.CreateOrgAdminAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // The base class already created and logged in TestUser — use them as the invitee
        string inviteeEmail = TestUser.Email;

        // Create an invitation for the invitee via the API
        await InvitationHelper.CreateInvitationAsync(
            Docker.ApiBaseUrl, orgAdmin.AuthCookie, inviteeEmail);

        // Retrieve the invitation link from the invitee's email
        string invitationLink = await InvitationHelper.SearchForInvitationLinkAsync(
            Docker.MailpitBaseUrl, inviteeEmail);
        Assert.False(string.IsNullOrEmpty(invitationLink), "Invitation email should contain a link");

        // Extract the token from the invitation link
        Uri uri = new(invitationLink);
        string? token = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("token");
        Assert.False(string.IsNullOrEmpty(token), "Invitation link should contain a token parameter");

        // Navigate to the invitation landing page (user is already authenticated)
        InvitationLandingPage landingPage = new(Page, Docker.AuthBaseUrl);
        await landingPage.NavigateAsync(token!);

        bool isLoaded = await landingPage.IsLoadedAsync();
        Assert.True(isLoaded, "Invitation landing page should be loaded");

        // Decline the invitation
        await landingPage.ClickDeclineAsync();

        // After declining, the user should be navigated away from the invitation page
        await Page.WaitForURLAsync(
            url => !url.Contains("/invitation", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 15_000 });

        Assert.DoesNotContain("/invitation", Page.Url, StringComparison.OrdinalIgnoreCase);

        // Verify the invitee is NOT in the org by navigating to the org detail page
        // A non-member should see not-found or an empty/unauthorized state
        OrganizationDetailPage detailPage = new(Page, Docker.WebBaseUrl);
        await Page.GotoAsync($"{Docker.WebBaseUrl}/dashboard/organizations/{orgAdmin.OrgId}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await E2ETestBase.WaitForBlazorReadyAsync(Page);

        bool isNotFound = await detailPage.IsNotFoundAsync();
        Assert.True(isNotFound, "Declined invitee should not have access to the organization");
    }
}
