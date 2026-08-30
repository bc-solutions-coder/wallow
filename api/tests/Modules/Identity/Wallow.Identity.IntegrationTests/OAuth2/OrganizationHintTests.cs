using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// The <c>organization</c> authorize parameter. A first-party client is bound to nothing and
/// names an organization per request; the transaction then runs that organization's enrollment
/// policy exactly as a bound client's does. Without a hint the token carries the user's single
/// membership or no organization at all. A bound client may only name its own organization.
/// </summary>
public sealed class OrganizationHintTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "OrgHint1234!";
    private const string ClientSecret = "org-hint-client-secret";
    private const string Scope = "openid profile email roles";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "roles"];

    [Fact]
    public async Task Authorize_FirstPartyWithAHint_ScopesTheTokenToTheHintedOrganization()
    {
        Seed seed = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(
            seed.FirstPartyClientId, ClientSecret, Scope, organization: seed.MemberOrganizationId.ToString());

        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
        string accessToken = tokens.RequireAccessToken();
        AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "org_id")
            .Should().BeEquivalentTo([seed.MemberOrganizationId.ToString()]);
        AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "role")
            .Should().BeEquivalentTo(["user"], "the hinted organization's membership decides the roles");
    }

    [Fact]
    public async Task Authorize_FirstPartyWithAHint_RunsTheHintedOrganizationsEnrollmentPolicy()
    {
        Seed seed = await SeedAsync();
        Guid strangerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"org-hint-stranger-{Guid.NewGuid():N}@wallow.dev", Password);
        Guid approvalOrganizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Org Hint Approval {Guid.NewGuid():N}", strangerId);
        await ScopedServices.GetRequiredService<IOrganizationService>().UpdateEnrollmentAsync(
            approvalOrganizationId, EnrollmentPolicy.RequestApproval, null, null, strangerId);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(
            seed.FirstPartyClientId, Scope, organization: approvalOrganizationId.ToString());

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        authorize.Location?.ToString().Should().EndWith("/access-request");
    }

    [Fact]
    public async Task Authorize_FirstPartyWithAHintTheUserCannotJoin_LandsOnTheErrorPage()
    {
        Seed seed = await SeedAsync();
        Guid strangerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"org-hint-closed-{Guid.NewGuid():N}@wallow.dev", Password);
        Guid inviteOnlyOrganizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Org Hint Closed {Guid.NewGuid():N}", strangerId);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(
            seed.FirstPartyClientId, Scope, organization: inviteOnlyOrganizationId.ToString());

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        authorize.Error.Should().Be("not_a_member");
    }

    [Fact]
    public async Task Authorize_FirstPartyWithoutAHint_ForAUserWithSeveralMemberships_CarriesNoOrganization()
    {
        Seed seed = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(seed.FirstPartyClientId, ClientSecret, Scope);

        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
        AuthorizationCodeFlowHarness.ReadClaimValues(tokens.RequireAccessToken(), "org_id")
            .Should().BeEmpty("a user who belongs to several organizations has picked none yet");
    }

    [Fact]
    public async Task Authorize_BoundClientWithAHintNamingAnotherOrganization_IsInvalidRequest()
    {
        Seed seed = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(
            seed.BoundClientId, Scope, organization: seed.MemberOrganizationId.ToString());

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        authorize.Location?.ToString().Should().StartWith(AuthorizationCodeFlowHarness.RedirectUri);
        authorize.Error.Should().Be("invalid_request");
    }

    [Fact]
    public async Task Authorize_BoundClientWithAHintNamingItsOwnOrganization_SignsIn()
    {
        Seed seed = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(
            seed.BoundClientId, Scope, organization: seed.AdminOrganizationId.ToString());

        // A bound client is explicit-consent, so signing in means reaching the consent screen.
        authorize.Error.Should().BeNull(authorize.Location?.ToString());
        authorize.ConsentToken.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// A user who owns one organization and is a plain member of another, a first-party client
    /// bound to nothing, and a third-party client bound to the organization they own.
    /// </summary>
    private async Task<Seed> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"org-hint-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid adminOrganizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Org Hint Admin {suffix}", userId);

        Guid outsiderId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"org-hint-owner-{suffix}@wallow.dev", Password);
        Guid memberOrganizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Org Hint Member {suffix}", outsiderId);
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, memberOrganizationId, userId, "user");

        string firstPartyClientId = $"platform-console-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, firstPartyClientId, ClientSecret, tenantId: null, _clientScopes, firstParty: true);

        string boundClientId = $"partner-app-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, boundClientId, ClientSecret, adminOrganizationId, _clientScopes);

        return new Seed(email, firstPartyClientId, boundClientId, adminOrganizationId, memberOrganizationId);
    }

    private sealed record Seed(
        string Email,
        string FirstPartyClientId,
        string BoundClientId,
        Guid AdminOrganizationId,
        Guid MemberOrganizationId);
}
