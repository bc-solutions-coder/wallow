using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// What a membership status is worth at the authorize endpoint, and again at every refresh. Only
/// Active signs a person in. Suspended and Denied refuse under their own reason so the auth app can
/// say what happened rather than only that access was refused; Pending is not a refusal at all and
/// goes to the request-submitted screen. Global admin is an authority no organization grants, so it
/// passes the gate holding no membership at all.
///
/// Suspending also reaches backwards: a token already in someone's hands stops working on the next
/// request, which is what the token-entry validation on the resource server buys.
/// </summary>
public sealed class MembershipStatusGateTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string ClientSecret = "membership-gate-client-secret";
    private const string Scope = "openid profile email roles";

    private static readonly string[] _clientScopes =
        ["openid", "profile", "email", "roles", "offline_access"];

    // The test host authenticates every other route through TestAuthHandler, which never consults
    // OpenIddict. Userinfo authenticates against the OpenIddict server scheme itself, so it is where
    // an issued access token is actually validated in-process. The absolute address matters: the
    // issuer is stamped from the host a token was minted through, and the harness signs in over
    // https://localhost.
    private static readonly Uri _userinfo = new("https://localhost/connect/userinfo");

    [Fact]
    public async Task Authorize_ForAnActiveMembership_IssuesAToken()
    {
        Seed seed = await SeedAsync();
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, seed.OrganizationId, seed.UserId, "user");

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(seed.ClientId, ClientSecret, Scope);

        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
    }

    [Fact]
    public async Task Authorize_ForAPendingMembership_IssuesNoCode()
    {
        Seed seed = await SeedAsync();
        await AddMembershipAsync(Membership.RequestAccess(
            seed.UserId, OrganizationId.Create(seed.OrganizationId), TimeProvider.System));

        AuthorizeOutcome authorize = await AuthorizeAsync(seed);

        authorize.Code.Should().BeNull(authorize.Location?.ToString());

        // A pending request is the one non-Active outcome that is not a refusal, so it lands on
        // its own screen and carries no reason for the error page to render.
        authorize.Location?.ToString().Should().EndWith("/access-request");
        authorize.Error.Should().BeNull();
    }

    [Fact]
    public async Task Authorize_ForASuspendedMembership_IssuesNoCode()
    {
        Seed seed = await SeedAsync();
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, seed.OrganizationId, seed.UserId, "user");

        await SuspendAsync(seed);

        AuthorizeOutcome authorize = await AuthorizeAsync(seed);

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        authorize.Error.Should().Be("membership_suspended");
    }

    [Fact]
    public async Task Authorize_ForADeniedMembership_IssuesNoCode()
    {
        Seed seed = await SeedAsync();
        Membership membership = Membership.RequestAccess(
            seed.UserId, OrganizationId.Create(seed.OrganizationId), TimeProvider.System);
        membership.Deny(seed.OwnerId, TimeProvider.System);
        await AddMembershipAsync(membership);

        AuthorizeOutcome authorize = await AuthorizeAsync(seed);

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        authorize.Error.Should().Be("membership_denied");
    }

    [Fact]
    public async Task Authorize_ForAGlobalAdminHoldingNoMembership_IssuesAToken()
    {
        Seed seed = await SeedAsync();

        UserManager<WallowUser> users = ScopedServices.GetRequiredService<UserManager<WallowUser>>();
        WallowUser user = await users.FindByIdAsync(seed.UserId.ToString())
            ?? throw new InvalidOperationException("The seeded user is missing.");
        IdentityResult granted = await users.AddClaimAsync(
            user, new Claim(ClaimsPrincipalExtensions.GlobalAdminClaimType, "true"));
        granted.Succeeded.Should().BeTrue();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(seed.ClientId, ClientSecret, Scope);

        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
    }

    [Fact]
    public async Task Refresh_AfterTheMembershipIsSuspended_IsRefused()
    {
        Seed seed = await SeedAsync();
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, seed.OrganizationId, seed.UserId, "user");

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(
            seed.ClientId, ClientSecret, $"{Scope} offline_access");
        tokens.RefreshToken.Should().NotBeNullOrEmpty(tokens.Body);

        await SuspendAsync(seed);

        TokenOutcome refreshed = await harness.RefreshAsync(
            seed.ClientId, ClientSecret, tokens.RefreshToken!);

        refreshed.AccessToken.Should().BeNull(refreshed.Body);
        refreshed.Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task Suspending_RejectsAnAccessTokenIssuedBeforeIt()
    {
        Seed seed = await SeedAsync();
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, seed.OrganizationId, seed.UserId, "user");

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(seed.ClientId, ClientSecret, Scope);
        tokens.AccessToken.Should().NotBeNullOrEmpty(tokens.Body);

        harness.Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokens.AccessToken}");

        HttpResponseMessage before = await harness.Client.GetAsync(_userinfo);
        before.StatusCode.Should().Be(HttpStatusCode.OK, await before.Content.ReadAsStringAsync());

        IMembershipReviewService review = ScopedServices.GetRequiredService<IMembershipReviewService>();
        await review.SuspendAsync(seed.OrganizationId, seed.UserId, seed.OwnerId);

        HttpResponseMessage after = await harness.Client.GetAsync(_userinfo);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized, await after.Content.ReadAsStringAsync());
    }

    private async Task SuspendAsync(Seed seed)
    {
        IMembershipRepository memberships = ScopedServices.GetRequiredService<IMembershipRepository>();
        Membership membership = await memberships.GetAsync(seed.UserId, seed.OrganizationId)
            ?? throw new InvalidOperationException("Enrolling the member wrote no membership.");

        membership.Suspend(seed.OwnerId, TimeProvider.System);
        await memberships.SaveChangesAsync();
    }

    private async Task<AuthorizeOutcome> AuthorizeAsync(Seed seed)
    {
        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);
        return await harness.AuthorizeAsync(seed.ClientId, Scope);
    }

    private async Task AddMembershipAsync(Membership membership)
    {
        IMembershipRepository memberships = ScopedServices.GetRequiredService<IMembershipRepository>();
        memberships.Add(membership);
        await memberships.SaveChangesAsync();
    }

    /// <summary>
    /// Someone else owns the organization, because creating one enrolls its creator as an active
    /// admin and there would then be no status left to set.
    /// </summary>
    private async Task<Seed> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"membership-gate-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, email, Password);

        Guid ownerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"membership-gate-owner-{suffix}@wallow.dev", Password);

        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Membership Gate {suffix}", ownerId);

        string clientId = $"wallow-membership-gate-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, organizationId, _clientScopes);

        return new Seed(email, clientId, userId, ownerId, organizationId);
    }

    private sealed record Seed(
        string Email,
        string ClientId,
        Guid UserId,
        Guid OwnerId,
        Guid OrganizationId);
}
