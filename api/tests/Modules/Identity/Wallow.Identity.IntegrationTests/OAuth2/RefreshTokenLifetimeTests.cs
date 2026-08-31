using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Tests.Common.Factories;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Per-client refresh-token lifetime, end to end: the lifetime a client is registered with (or
/// the pinned first-party/third-party default) bounds the refresh tokens the server stores for
/// it, changing it later leaves tokens already issued alone, and — with sliding expiration
/// pinned off — a refreshed token inherits its family's original expiry. Also pins reuse
/// detection: replaying a redeemed refresh token inside the leeway is a benign retry, beyond it
/// the whole authorization family is revoked. The test factory shrinks the leeway to 2 s and
/// moves the global fallback to 5 days so each pinned default is distinguishable from it.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RefreshTokenLifetimeTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string FullScope = "openid profile email offline_access";
    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    /// <summary>
    /// Storage timestamps trail issuance by however long the request took, so the stored
    /// window is asserted to the nearest few seconds, never exactly.
    /// </summary>
    private static readonly TimeSpan _storedWindowTolerance = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ClientRegisteredWithASixtySecondLifetime_GetsASixtySecondRefreshToken()
    {
        Seed seed = await SeedOrgClientAsync(refreshTokenLifetime: 60);
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);

        TokenOutcome issued = await AcquireWithConsentAsync(harness, seed);

        issued.RefreshToken.Should().NotBeNull(issued.Body);
        StoredToken stored = await NewestValidRefreshTokenAsync(seed.UserId, seed.ClientId);
        (stored.Expires - stored.Created).Should().BeCloseTo(
            TimeSpan.FromSeconds(60), _storedWindowTolerance);
    }

    [Fact]
    public async Task OrgRegisteredClientWithoutALifetime_GetsTheThirdPartyDayDefault()
    {
        Seed seed = await SeedOrgClientAsync(refreshTokenLifetime: null);
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);

        TokenOutcome issued = await AcquireWithConsentAsync(harness, seed);

        issued.RefreshToken.Should().NotBeNull(issued.Body);
        StoredToken stored = await NewestValidRefreshTokenAsync(seed.UserId, seed.ClientId);
        (stored.Expires - stored.Created).Should().BeCloseTo(
            TimeSpan.FromSeconds(ClientRefreshTokenLifetimes.ThirdPartyDefaultSeconds),
            _storedWindowTolerance);
    }

    [Fact]
    public async Task SeededFirstPartyClientWithoutALifetime_GetsTheSevenDayDefault()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"rtl-fp-{suffix}@wallow.dev";
        string clientId = $"rtl-fp-{suffix}";
        const string clientSecret = "rtl-first-party-secret";

        // Registered the way deployments register first-party clients: through the seed sync,
        // whose pinned default is under test. The factory moves the global fallback to 5 days,
        // so a 7-day window can only have come from the explicit per-client setting.
        PreRegisteredClientOptions options = new()
        {
            Clients =
            {
                new PreRegisteredClientDefinition
                {
                    ClientId = clientId,
                    DisplayName = "Lifetime first-party",
                    Secret = clientSecret,
                    FirstParty = true,
                    RedirectUris = { AuthorizationCodeFlowHarness.RedirectUri },
                    Scopes = { "openid", "profile", "email", "offline_access" },
                },
            },
        };
        PreRegisteredClientSyncService sync = new(
            ScopedServices.GetRequiredService<IOpenIddictApplicationManager>(),
            ScopedServices.GetRequiredService<IOrganizationService>(),
            ScopedServices.GetRequiredService<UserManager<WallowUser>>(),
            Options.Create(options),
            NullLogger<PreRegisteredClientSyncService>.Instance);
        await sync.SyncAsync(CancellationToken.None);

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Lifetime FP {suffix}", userId);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);
        TokenOutcome issued = await harness.AcquireTokensAsync(
            clientId, clientSecret, FullScope, organization: organizationId.ToString());

        issued.RefreshToken.Should().NotBeNull(issued.Body);
        StoredToken stored = await NewestValidRefreshTokenAsync(userId, clientId);
        (stored.Expires - stored.Created).Should().BeCloseTo(
            TimeSpan.FromSeconds(ClientRefreshTokenLifetimes.FirstPartyDefaultSeconds),
            _storedWindowTolerance);
    }

    [Fact]
    public async Task ChangingTheLifetime_LeavesAnExistingRefreshTokenAlone()
    {
        Seed seed = await SeedOrgClientAsync(refreshTokenLifetime: 3600);
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        TokenOutcome issued = await AcquireWithConsentAsync(harness, seed);
        issued.RefreshToken.Should().NotBeNull(issued.Body);
        StoredToken original = await NewestValidRefreshTokenAsync(seed.UserId, seed.ClientId);

        IOrganizationClientService clients = ScopedServices.GetRequiredService<IOrganizationClientService>();
        OrganizationClientDto? updated = await clients.UpdateAsync(
            seed.OrganizationId,
            seed.ClientId,
            new ClientConfigurationInput(
                [new Uri(AuthorizationCodeFlowHarness.RedirectUri)], [], null, _clientScopes,
                RefreshTokenLifetime: 60));
        updated.Should().NotBeNull();
        updated!.RefreshTokenLifetime.Should().Be(60);

        StoredToken afterUpdate = await NewestValidRefreshTokenAsync(seed.UserId, seed.ClientId);
        afterUpdate.Expires.Should().Be(
            original.Expires, "changing the client's lifetime must not reshape tokens already issued");

        // Sliding expiration is pinned off, so the refreshed token inherits the family's
        // original expiry instead of starting a fresh 60-second (or 3600-second) window.
        TokenOutcome refreshed = await harness.RefreshAsync(
            seed.ClientId, seed.ClientSecret, issued.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK, refreshed.Body);
        StoredToken descendant = await NewestValidRefreshTokenAsync(seed.UserId, seed.ClientId);
        descendant.Expires.Should().BeCloseTo(original.Expires, _storedWindowTolerance);
    }

    [Fact]
    public async Task ReplayingARedeemedRefreshToken_WithinTheLeeway_IsToleratedAsARetry()
    {
        Seed seed = await SeedOrgClientAsync(refreshTokenLifetime: null);
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        TokenOutcome issued = await AcquireWithConsentAsync(harness, seed);
        issued.RefreshToken.Should().NotBeNull(issued.Body);

        TokenOutcome first = await harness.RefreshAsync(
            seed.ClientId, seed.ClientSecret, issued.RefreshToken!);
        first.StatusCode.Should().Be(HttpStatusCode.OK, first.Body);

        TokenOutcome replay = await harness.RefreshAsync(
            seed.ClientId, seed.ClientSecret, issued.RefreshToken!);
        replay.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "an immediate replay is a benign concurrent retry inside the leeway: " + replay.Body);
    }

    [Fact]
    public async Task ReplayingARedeemedRefreshToken_AfterTheLeeway_RevokesTheWholeFamily()
    {
        Seed seed = await SeedOrgClientAsync(refreshTokenLifetime: null);
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        TokenOutcome issued = await AcquireWithConsentAsync(harness, seed);
        issued.RefreshToken.Should().NotBeNull(issued.Body);

        TokenOutcome successor = await harness.RefreshAsync(
            seed.ClientId, seed.ClientSecret, issued.RefreshToken!);
        successor.StatusCode.Should().Be(HttpStatusCode.OK, successor.Body);
        successor.RefreshToken.Should().NotBeNull(successor.Body);

        // The factory pins the leeway to 2 s; sleeping past it turns the replay from a
        // tolerated retry into theft evidence.
        await Task.Delay(TimeSpan.FromSeconds(3.5));

        TokenOutcome replay = await harness.RefreshAsync(
            seed.ClientId, seed.ClientSecret, issued.RefreshToken!);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest, replay.Body);
        replay.Error.Should().Be(Errors.InvalidGrant, replay.Body);

        TokenOutcome descendant = await harness.RefreshAsync(
            seed.ClientId, seed.ClientSecret, successor.RefreshToken!);
        descendant.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "reuse beyond the leeway revokes every token in the authorization family: " + descendant.Body);
    }

    /// <summary>
    /// Registers an application client through the organization surface — the path whose default
    /// and explicit lifetimes are under test — owned by someone other than the signing-in user.
    /// </summary>
    private async Task<Seed> SeedOrgClientAsync(int? refreshTokenLifetime)
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"rtl-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid ownerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"rtl-owner-{suffix}@wallow.dev", Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Lifetime {suffix}", ownerId);

        IOrganizationClientService clients = ScopedServices.GetRequiredService<IOrganizationClientService>();
        OrganizationClientRegistrationResult registered = await clients.RegisterAsync(
            organizationId,
            new RegisterClientInput(
                RegisteredClientKind.Application,
                $"Lifetime App {suffix}",
                new ClientConfigurationInput(
                    [new Uri(AuthorizationCodeFlowHarness.RedirectUri)],
                    [],
                    null,
                    _clientScopes,
                    RefreshTokenLifetime: refreshTokenLifetime)),
            ownerId);

        return new Seed(email, registered.Client.ClientId, registered.ClientSecret, userId, organizationId);
    }

    private async Task<AuthorizationCodeFlowHarness> SignedInAsync(Seed seed)
    {
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, seed.OrganizationId, seed.UserId, "user");

        AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);
        return harness;
    }

    /// <summary>An org-registered client is explicit-consent, so acquiring tokens walks the consent screen.</summary>
    private static async Task<TokenOutcome> AcquireWithConsentAsync(
        AuthorizationCodeFlowHarness harness,
        Seed seed)
    {
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, FullScope);
        AuthorizeOutcome granted = await harness.ConsentAsync(consent, grant: true);
        granted.Code.Should().NotBeNull(granted.Location?.ToString() ?? granted.Body);
        return await harness.ExchangeCodeAsync(
            seed.ClientId, seed.ClientSecret, granted.Code!, granted.CodeVerifier);
    }

    /// <summary>
    /// The newest refresh-token row the server holds for (user, client) — the stored record is
    /// the source of truth for expiry, since the refresh token on the wire is opaque.
    /// </summary>
    private async Task<StoredToken> NewestValidRefreshTokenAsync(Guid userId, string clientId)
    {
        IOpenIddictApplicationManager applications =
            ScopedServices.GetRequiredService<IOpenIddictApplicationManager>();
        IOpenIddictTokenManager tokens = ScopedServices.GetRequiredService<IOpenIddictTokenManager>();

        object application = await applications.FindByClientIdAsync(clientId)
            ?? throw new InvalidOperationException($"Client '{clientId}' is not registered.");
        string applicationId = await applications.GetIdAsync(application)
            ?? throw new InvalidOperationException($"Client '{clientId}' has no id.");

        StoredToken? newest = null;
        await foreach (object token in tokens.FindAsync(
            userId.ToString(), applicationId, Statuses.Valid, TokenTypeIdentifiers.RefreshToken))
        {
            DateTimeOffset? created = await tokens.GetCreationDateAsync(token);
            DateTimeOffset? expires = await tokens.GetExpirationDateAsync(token);
            if (created is null || expires is null)
            {
                continue;
            }

            if (newest is null || created > newest.Created)
            {
                newest = new StoredToken(created.Value, expires.Value);
            }
        }

        return newest
            ?? throw new InvalidOperationException(
                $"No valid refresh token is stored for user '{userId}' and client '{clientId}'.");
    }

    private sealed record Seed(
        string Email,
        string ClientId,
        string ClientSecret,
        Guid UserId,
        Guid OrganizationId);

    private sealed record StoredToken(DateTimeOffset Created, DateTimeOffset Expires);
}
