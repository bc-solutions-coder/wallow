using System.Net;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Checks POST consent tokens bound to the user and request, including missing, replayed,
/// and mismatched tokens and decisions supplied on GET.
/// </summary>
public sealed class ConsentTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string ClientSecret = "consent-client-secret";
    private const string Scope = "openid profile email";
    private const string ConsentPath = "/consent";

    private static readonly string[] _clientScopes = ["openid", "profile", "email"];

    [Fact]
    public async Task Authorize_ForAnExplicitConsentClient_SendsTheUserToConsentWithAToken()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(seed.ClientId, Scope);

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        PathOf(authorize.Location).Should().Be(ConsentPath);
        authorize.ReturnUrl.Should().StartWith("/connect/authorize?");
        authorize.ConsentToken.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("consent_granted=true")]
    [InlineData("consent_decision=granted")]
    public async Task Authorize_WithAConsentFlagOnTheGet_StillShowsTheConsentScreen(string flag)
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(seed.ClientId, Scope, extraQuery: flag);

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        PathOf(authorize.Location).Should().Be(ConsentPath);
        authorize.ConsentToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Consent_Granted_IssuesACodeTheTokenEndpointAccepts()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, Scope);

        AuthorizeOutcome granted = await harness.ConsentAsync(consent, grant: true);

        granted.Code.Should().NotBeNull(granted.Location?.ToString() ?? granted.Body);
        TokenOutcome tokens = await harness.ExchangeCodeAsync(
            seed.ClientId, ClientSecret, granted.Code!, granted.CodeVerifier);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
    }

    [Fact]
    public async Task Consent_Granted_IsRememberedOnTheNextAuthorize()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, Scope);
        await harness.ConsentAsync(consent, grant: true);

        AuthorizeOutcome again = await harness.AuthorizeAsync(seed.ClientId, Scope);

        again.Code.Should().NotBeNull(again.Location?.ToString());
    }

    [Fact]
    public async Task Consent_Denied_RefusesTheClientWithConsentRequired()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, Scope);

        AuthorizeOutcome denied = await harness.ConsentAsync(consent, grant: false);

        denied.Code.Should().BeNull(denied.Location?.ToString());
        denied.Error.Should().Be("consent_required");
    }

    [Fact]
    public async Task Consent_WithoutAToken_IssuesNoCode()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, Scope);

        AuthorizeOutcome outcome = await harness.ConsentAsync(consent, grant: true, consentToken: string.Empty);

        ShouldBeBackOnConsent(outcome);
    }

    [Fact]
    public async Task Consent_WithAReplayedToken_IssuesNoCode()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, Scope);
        await harness.ConsentAsync(consent, grant: false);

        AuthorizeOutcome replay = await harness.ConsentAsync(consent, grant: true);

        ShouldBeBackOnConsent(replay);
    }

    [Fact]
    public async Task Consent_WithATokenMintedForAnotherUser_IssuesNoCode()
    {
        Seed seed = await SeedAsync();
        Seed other = await SeedAsync();
        using AuthorizationCodeFlowHarness victim = await SignedInAsync(seed);
        using AuthorizationCodeFlowHarness attacker = await SignedInAsync(other, seed);
        AuthorizeOutcome attackerConsent = await attacker.AuthorizeAsync(seed.ClientId, Scope);
        AuthorizeOutcome victimConsent = await victim.AuthorizeAsync(seed.ClientId, Scope);

        AuthorizeOutcome outcome = await victim.ConsentAsync(
            victimConsent, grant: true, consentToken: attackerConsent.ConsentToken);

        ShouldBeBackOnConsent(outcome);
    }

    [Fact]
    public async Task Consent_WithATokenMintedForAnotherRequest_IssuesNoCode()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome narrow = await harness.AuthorizeAsync(seed.ClientId, "openid");
        AuthorizeOutcome wide = await harness.AuthorizeAsync(seed.ClientId, Scope);

        AuthorizeOutcome outcome = await harness.ConsentAsync(
            wide, grant: true, consentToken: narrow.ConsentToken);

        ShouldBeBackOnConsent(outcome);
    }

    /// <summary>
    /// Requires a consent redirect with a token and no authorization code or protocol error.
    /// </summary>
    private static void ShouldBeBackOnConsent(AuthorizeOutcome outcome)
    {
        outcome.Code.Should().BeNull(outcome.Location?.ToString() ?? outcome.Body);
        outcome.Error.Should().BeNull(outcome.Location?.ToString());
        PathOf(outcome.Location).Should().Be(ConsentPath);
        outcome.ConsentToken.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Extracts the path from an absolute or relative redirect.
    /// </summary>
    private static string PathOf(Uri? location)
    {
        location.Should().NotBeNull("the endpoint should have redirected");
        string target = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        int query = target.IndexOf('?', StringComparison.Ordinal);
        return query >= 0 ? target[..query] : target;
    }

    /// <summary>
    /// Signs in the seeded user after enrolling them in the selected client organization.
    /// </summary>
    private async Task<AuthorizationCodeFlowHarness> SignedInAsync(Seed seed, Seed? clientOwner = null)
    {
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, (clientOwner ?? seed).OrganizationId, seed.UserId, "user");

        AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);
        return harness;
    }

    private async Task<Seed> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"consent-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid ownerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"consent-owner-{suffix}@wallow.dev", Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Consent {suffix}", ownerId);

        // The registration below defaults to explicit consent.
        string clientId = $"consent-app-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, organizationId, _clientScopes);

        return new Seed(email, clientId, userId, organizationId);
    }

    private sealed record Seed(string Email, string ClientId, Guid UserId, Guid OrganizationId);
}
