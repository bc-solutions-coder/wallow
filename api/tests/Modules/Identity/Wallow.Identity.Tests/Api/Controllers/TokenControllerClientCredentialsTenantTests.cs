using System.Collections.Immutable;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// Where a client-credentials token gets its tenant. Only the OpenIddict application record may
/// name it: a client id is chosen by whoever registered the service account, so deriving a tenant
/// from it hands an authorization input to the caller. A record that names no tenant yields no
/// tenant claim, and permission expansion then grants that principal nothing.
/// </summary>
public sealed class TokenControllerClientCredentialsTenantTests : IDisposable
{
    /// <summary>
    /// The OpenIddict application property the tenant must come from — the sibling of
    /// <c>wallow:is_operator</c>, which the same handler already resolves this way.
    /// </summary>
    private const string TenantPropertyName = "wallow:tenant_id";

    /// <summary>
    /// The one spelling <c>ClaimsPrincipalExtensions.GetTenantId</c> reads, and therefore the one
    /// spelling that resolves a tenant for permission expansion.
    /// </summary>
    private const string TenantClaimType = "org_id";

    private readonly UserManager<WallowUser> _userManager;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly TokenController _controller;

    public TokenControllerClientCredentialsTenantTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();

        _controller = new TokenController(
            _userManager,
            _applicationManager,
            Substitute.For<IMembershipRoleResolver>(),
            NullLogger<TokenController>.Instance);
    }

    public void Dispose()
    {
        _controller.Dispose();
        _userManager.Dispose();
    }

    [Fact]
    public async Task Exchange_ClientCredentials_ShouldNotDeriveTheTenant_FromTheClientIdString()
    {
        ArrangeClientCredentials("sa-acme-worker");

        IActionResult result = await _controller.Exchange();

        ClaimsPrincipal principal = ResultPrincipal(result);

        principal.FindFirst(TenantClaimType).Should().BeNull(
            "the application record carries no tenant, so the token must carry no tenant claim. " +
            "Splitting the client id yields 'acme' — a value the person who named the service " +
            "account chose, which is an authorization input a caller must never control");
    }

    [Fact]
    public async Task Exchange_ClientCredentials_ShouldNotEmit_ATruncatedTenantId()
    {
        Guid tenantId = Guid.NewGuid();
        ArrangeClientCredentials($"sa-{tenantId.ToString("D", CultureInfo.InvariantCulture)}-worker");

        IActionResult result = await _controller.Exchange();

        Claim? tenantClaim = ResultPrincipal(result).FindFirst(TenantClaimType);

        if (tenantClaim is not null)
        {
            Guid.TryParse(tenantClaim.Value, out _).Should().BeTrue(
                "a tenant id is a Guid, and a Guid contains dashes — so splitting the client id " +
                "on '-' returns only the Guid's first block. '{0}' for client id 'sa-{1}-worker' " +
                "resolves to no tenant and matches no row",
                tenantClaim.Value,
                tenantId);
        }
    }

    [Fact]
    public async Task Exchange_ClientCredentials_ShouldEmit_TheTenantRecordedOnTheApplication()
    {
        Guid tenantId = Guid.NewGuid();
        ArrangeClientCredentials(
            "sa-acme-worker",
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [TenantPropertyName] = JsonSerializer.SerializeToElement(
                    tenantId.ToString("D", CultureInfo.InvariantCulture))
            });

        IActionResult result = await _controller.Exchange();

        Claim? tenantClaim = ResultPrincipal(result).FindFirst(TenantClaimType);

        tenantClaim.Should().NotBeNull(
            "when the application record does declare a tenant, the token must carry it — that " +
            "record is the only trustworthy source, exactly as it already is for the operator flag");
        tenantClaim.Value.Should().Be(
            tenantId.ToString("D", CultureInfo.InvariantCulture),
            "the claim must be the tenant the application record names, whole and unparsed");
    }

    private void ArrangeClientCredentials(
        string clientId,
        IReadOnlyDictionary<string, JsonElement>? applicationProperties = null)
    {
        DefaultHttpContext httpContext = new();
        OpenIddictServerTransaction transaction = new()
        {
            Request = new OpenIddictRequest
            {
                GrantType = GrantTypes.ClientCredentials,
                ClientId = clientId
            }
        };
        httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });

        object application = new();
        _applicationManager
            .FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(application);
        _applicationManager
            .GetPropertiesAsync(application, Arg.Any<CancellationToken>())
            .Returns(applicationProperties is null
                ? ImmutableDictionary<string, JsonElement>.Empty
                : applicationProperties.ToImmutableDictionary(StringComparer.Ordinal));

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private static ClaimsPrincipal ResultPrincipal(IActionResult result)
    {
        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        return signIn.Principal;
    }
}
