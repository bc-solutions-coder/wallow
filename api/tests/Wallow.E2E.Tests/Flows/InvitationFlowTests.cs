using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
[Trait("E2EGroup", "Invitations")]
public sealed class InvitationFlowTests : E2ETestBase
{
    public InvitationFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    [Fact]
    public async Task InvitationLanding_WithNoToken_ShowsError()
    {
        InvitationLandingPage landingPage = new(Page, Docker.AuthBaseUrl);
        await landingPage.NavigateAsync(string.Empty);

        bool isError = await landingPage.IsErrorVisibleAsync();
        Assert.True(isError, "Invitation landing with no token should show an error");
    }

    [Fact]
    public async Task InvitationLanding_WithInvalidToken_ShowsError()
    {
        InvitationLandingPage landingPage = new(Page, Docker.AuthBaseUrl);
        await landingPage.NavigateAsync($"invalid-token-{Guid.NewGuid():N}");

        bool isError = await landingPage.IsErrorVisibleAsync();
        Assert.True(isError, "Invitation landing with invalid token should show an error");
    }

    [Fact]
    public async Task InvitationLanding_WithValidToken_UnauthenticatedUser_ShowsSignInPrompt()
    {
        OrgAdminTestUser orgAdmin = await TestUserFactory.CreateOrgAdminAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        string inviteeEmail = $"invitee-{Guid.NewGuid():N}@test.local";
        await InvitationHelper.CreateInvitationAsync(
            Docker.ApiBaseUrl, orgAdmin.AuthCookie, inviteeEmail);

        string invitationLink = await InvitationHelper.SearchForInvitationLinkAsync(
            Docker.MailpitBaseUrl, inviteeEmail);
        Assert.False(string.IsNullOrEmpty(invitationLink), "Invitation email should contain a link");

        // Extract the token from the invitation link
        Uri uri = new(invitationLink);
        string? token = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("token");
        Assert.False(string.IsNullOrEmpty(token), "Invitation link should contain a token parameter");

        InvitationLandingPage landingPage = new(Page, Docker.AuthBaseUrl);
        await landingPage.NavigateAsync(token!);

        bool signInVisible = await landingPage.IsSignInPromptVisibleAsync();
        Assert.True(signInVisible, "Unauthenticated user should see a sign-in prompt on invitation landing");
    }

    [Fact]
    public async Task InvitationLanding_SignInLink_ContainsReturnUrl()
    {
        OrgAdminTestUser orgAdmin = await TestUserFactory.CreateOrgAdminAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        string inviteeEmail = $"invitee-{Guid.NewGuid():N}@test.local";
        await InvitationHelper.CreateInvitationAsync(
            Docker.ApiBaseUrl, orgAdmin.AuthCookie, inviteeEmail);

        string invitationLink = await InvitationHelper.SearchForInvitationLinkAsync(
            Docker.MailpitBaseUrl, inviteeEmail);
        Assert.False(string.IsNullOrEmpty(invitationLink), "Invitation email should contain a link");

        Uri uri = new(invitationLink);
        string? token = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("token");
        Assert.False(string.IsNullOrEmpty(token), "Invitation link should contain a token parameter");

        InvitationLandingPage landingPage = new(Page, Docker.AuthBaseUrl);
        await landingPage.NavigateAsync(token!);

        string? signInHref = await landingPage.GetSignInHrefAsync();
        Assert.False(string.IsNullOrEmpty(signInHref), "Sign-in link should have an href");
        Assert.Contains("invitation", signInHref!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvitationLanding_CreateAccountLink_ContainsInviteeEmail()
    {
        OrgAdminTestUser orgAdmin = await TestUserFactory.CreateOrgAdminAsync(
            Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        string inviteeEmail = $"invitee-{Guid.NewGuid():N}@test.local";
        await InvitationHelper.CreateInvitationAsync(
            Docker.ApiBaseUrl, orgAdmin.AuthCookie, inviteeEmail);

        string invitationLink = await InvitationHelper.SearchForInvitationLinkAsync(
            Docker.MailpitBaseUrl, inviteeEmail);
        Assert.False(string.IsNullOrEmpty(invitationLink), "Invitation email should contain a link");

        Uri uri = new(invitationLink);
        string? token = System.Web.HttpUtility.ParseQueryString(uri.Query).Get("token");
        Assert.False(string.IsNullOrEmpty(token), "Invitation link should contain a token parameter");

        InvitationLandingPage landingPage = new(Page, Docker.AuthBaseUrl);
        await landingPage.NavigateAsync(token!);

        string? createAccountHref = await landingPage.GetCreateAccountHrefAsync();
        Assert.False(string.IsNullOrEmpty(createAccountHref), "Create account link should have an href");
        Assert.Contains(Uri.EscapeDataString(inviteeEmail), createAccountHref!, StringComparison.OrdinalIgnoreCase);
    }
}
