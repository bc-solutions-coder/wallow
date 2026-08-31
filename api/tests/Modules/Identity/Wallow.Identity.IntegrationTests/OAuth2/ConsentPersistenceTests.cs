using System.Collections.Immutable;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Consent, once granted, is a durable record: ONE permanent authorization per user and client
/// that every token chains to. A later request covered by that record issues a code without a
/// screen; a request for more asks only for the missing scopes and widens the record rather than
/// minting a second one; <c>prompt=consent</c> re-asks and <c>prompt=none</c> refuses with
/// <c>consent_required</c> instead of rendering UI. Only a permanent record satisfies consent —
/// the ad-hoc authorizations first-party sign-ins mint are per-login bookkeeping, not consent.
/// </summary>
public sealed class ConsentPersistenceTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string ClientSecret = "consent-persistence-secret";
    private const string NarrowScope = "openid profile";
    private const string WideScope = "openid profile email";
    private const string ConsentPath = "/consent";

    /// <summary>The refresh grant only runs under <c>offline_access</c>, so the client registers it.</summary>
    private const string RefreshScope = WideScope + " offline_access";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    [Fact]
    public async Task SecondAuthorize_WithTheSameScopes_ChainsToTheSamePermanentAuthorization()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, WideScope);
        AuthorizeOutcome granted = await harness.ConsentAsync(consent, grant: true);
        TokenOutcome first = await harness.ExchangeCodeAsync(
            seed.ClientId, ClientSecret, granted.Code!, granted.CodeVerifier);

        AuthorizeOutcome again = await harness.AuthorizeAsync(seed.ClientId, WideScope);

        again.Code.Should().NotBeNull(again.Location?.ToString());
        TokenOutcome second = await harness.ExchangeCodeAsync(
            seed.ClientId, ClientSecret, again.Code!, again.CodeVerifier);

        string firstAuthorizationId = AuthorizationIdOf(first);
        AuthorizationIdOf(second).Should().Be(
            firstAuthorizationId, "every sign-in chains to the one permanent consent record");

        List<StoredAuthorization> permanents = await PermanentAuthorizationsAsync(seed);
        permanents.Should().ContainSingle().Which.Id.Should().Be(firstAuthorizationId);
    }

    [Fact]
    public async Task GrownScopeSet_AsksOnlyForTheNewScopes_AndStoresTheUnion()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome narrowConsent = await harness.AuthorizeAsync(seed.ClientId, NarrowScope);
        await harness.ConsentAsync(narrowConsent, grant: true);

        AuthorizeOutcome wide = await harness.AuthorizeAsync(seed.ClientId, WideScope);

        PathOf(wide.Location).Should().Be(ConsentPath);
        ConsentScopeOf(wide.Location).Should().Be(
            "email", "the screen asks only for what the stored consent does not already cover");

        AuthorizeOutcome granted = await harness.ConsentAsync(wide, grant: true);
        granted.Code.Should().NotBeNull(granted.Location?.ToString() ?? granted.Body);

        List<StoredAuthorization> permanents = await PermanentAuthorizationsAsync(seed);
        StoredAuthorization union = permanents.Should().ContainSingle(
            "scope growth widens the one record instead of minting a second").Subject;
        union.Scopes.Should().BeEquivalentTo("openid", "profile", "email");
    }

    [Fact]
    public async Task AValidAdHocAuthorization_DoesNotSatisfyConsent()
    {
        Seed seed = await SeedAsync();
        await SeedAdHocAuthorizationAsync(seed, WideScope.Split(' '));
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(seed.ClientId, WideScope);

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        PathOf(authorize.Location).Should().Be(
            ConsentPath, "only a permanent authorization records consent; ad-hoc ones are per-login bookkeeping");
    }

    [Fact]
    public async Task PromptConsent_ForcesTheScreen_AndTheDecisionStillWins()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, WideScope);
        await harness.ConsentAsync(consent, grant: true);

        AuthorizeOutcome forced = await harness.AuthorizeAsync(
            seed.ClientId, WideScope, extraQuery: "prompt=consent");

        forced.Code.Should().BeNull(forced.Location?.ToString());
        PathOf(forced.Location).Should().Be(ConsentPath);
        forced.ConsentToken.Should().NotBeNullOrEmpty();

        // The POSTed decision must override the prompt=consent riding in the returnUrl,
        // or the answer would bounce back to the screen forever.
        AuthorizeOutcome answered = await harness.ConsentAsync(forced, grant: true);
        answered.Code.Should().NotBeNull(answered.Location?.ToString() ?? answered.Body);
    }

    [Fact]
    public async Task PromptNone_WithoutStoredConsent_FailsWithConsentRequired()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);

        AuthorizeOutcome outcome = await harness.AuthorizeAsync(
            seed.ClientId, WideScope, extraQuery: "prompt=none");

        outcome.Code.Should().BeNull(outcome.Location?.ToString());
        outcome.Error.Should().Be("consent_required");
    }

    [Fact]
    public async Task PromptNone_WithStoredConsent_IssuesACodeWithoutAScreen()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, WideScope);
        await harness.ConsentAsync(consent, grant: true);

        AuthorizeOutcome outcome = await harness.AuthorizeAsync(
            seed.ClientId, WideScope, extraQuery: "prompt=none");

        outcome.Code.Should().NotBeNull(outcome.Location?.ToString());
    }

    [Fact]
    public async Task AccessAndRefreshTokens_CarryThePermanentAuthorizationId()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, RefreshScope);
        AuthorizeOutcome granted = await harness.ConsentAsync(consent, grant: true);
        TokenOutcome tokens = await harness.ExchangeCodeAsync(
            seed.ClientId, ClientSecret, granted.Code!, granted.CodeVerifier);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);

        List<StoredAuthorization> permanents = await PermanentAuthorizationsAsync(seed);
        string permanentId = permanents.Should().ContainSingle().Subject.Id;

        AuthorizationIdOf(tokens).Should().Be(permanentId);

        tokens.RefreshToken.Should().NotBeNull(tokens.Body);
        TokenOutcome refreshed = await harness.RefreshAsync(
            seed.ClientId, ClientSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK, refreshed.Body);
        AuthorizationIdOf(refreshed).Should().Be(
            permanentId, "the refresh grant keeps tokens chained to the same consent record");
    }

    /// <summary>The authorization id an access token chains to.</summary>
    private static string AuthorizationIdOf(TokenOutcome tokens)
    {
        IReadOnlyList<string> values = AuthorizationCodeFlowHarness.ReadClaimValues(
            tokens.RequireAccessToken(), OpenIddictConstants.Claims.Private.AuthorizationId);
        values.Should().ContainSingle("access tokens must chain to an authorization");
        return values[0];
    }

    /// <summary>The <c>scope</c> the consent redirect asks the screen to describe.</summary>
    private static string? ConsentScopeOf(Uri? location)
    {
        location.Should().NotBeNull("the endpoint should have redirected");
        string target = location.IsAbsoluteUri ? location.Query : location.OriginalString;
        int separator = target.IndexOf('?', StringComparison.Ordinal);
        Dictionary<string, StringValues> parsed = QueryHelpers.ParseQuery(
            separator >= 0 ? target[separator..] : target);
        return parsed.TryGetValue("scope", out StringValues values) ? values.ToString() : null;
    }

    private static string PathOf(Uri? location)
    {
        location.Should().NotBeNull("the endpoint should have redirected");
        string target = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        int query = target.IndexOf('?', StringComparison.Ordinal);
        return query >= 0 ? target[..query] : target;
    }

    /// <summary>Every Valid permanent authorization the seeded user holds for the seeded client.</summary>
    private async Task<List<StoredAuthorization>> PermanentAuthorizationsAsync(Seed seed)
    {
        IOpenIddictAuthorizationManager authorizations =
            ScopedServices.GetRequiredService<IOpenIddictAuthorizationManager>();
        string applicationId = await ApplicationIdAsync(seed.ClientId);

        List<StoredAuthorization> found = [];
        await foreach (object authorization in authorizations.FindBySubjectAsync(seed.UserId.ToString()))
        {
            if (await authorizations.GetApplicationIdAsync(authorization) != applicationId
                || await authorizations.GetStatusAsync(authorization) != OpenIddictConstants.Statuses.Valid
                || await authorizations.GetTypeAsync(authorization) != OpenIddictConstants.AuthorizationTypes.Permanent)
            {
                continue;
            }

            ImmutableArray<string> scopes = await authorizations.GetScopesAsync(authorization);
            found.Add(new StoredAuthorization(
                (await authorizations.GetIdAsync(authorization))!, scopes));
        }

        return found;
    }

    /// <summary>
    /// Plants a Valid AD-HOC authorization covering the full scope set, the shape a first-party
    /// sign-in records — what the consent lookup must NOT accept as stored consent.
    /// </summary>
    private async Task SeedAdHocAuthorizationAsync(Seed seed, IEnumerable<string> scopes)
    {
        IOpenIddictAuthorizationManager authorizations =
            ScopedServices.GetRequiredService<IOpenIddictAuthorizationManager>();

        OpenIddictAuthorizationDescriptor descriptor = new()
        {
            ApplicationId = await ApplicationIdAsync(seed.ClientId),
            CreationDate = DateTimeOffset.UtcNow,
            Status = OpenIddictConstants.Statuses.Valid,
            Subject = seed.UserId.ToString(),
            Type = OpenIddictConstants.AuthorizationTypes.AdHoc,
        };

        foreach (string scope in scopes)
        {
            descriptor.Scopes.Add(scope);
        }

        await authorizations.CreateAsync(descriptor);
    }

    private async Task<string> ApplicationIdAsync(string clientId)
    {
        IOpenIddictApplicationManager applications =
            ScopedServices.GetRequiredService<IOpenIddictApplicationManager>();
        object application = (await applications.FindByClientIdAsync(clientId))!;
        return (await applications.GetIdAsync(application))!;
    }

    private async Task<AuthorizationCodeFlowHarness> SignedInAsync(Seed seed)
    {
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, seed.OrganizationId, seed.UserId, "user");

        AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);
        return harness;
    }

    private async Task<Seed> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"persist-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid ownerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"persist-owner-{suffix}@wallow.dev", Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Persist {suffix}", ownerId);

        string clientId = $"persist-app-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, organizationId, _clientScopes);

        return new Seed(email, clientId, userId, organizationId);
    }

    private sealed record Seed(string Email, string ClientId, Guid UserId, Guid OrganizationId);

    private sealed record StoredAuthorization(string Id, ImmutableArray<string> Scopes);
}
