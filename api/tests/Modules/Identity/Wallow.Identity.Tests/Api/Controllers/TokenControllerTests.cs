using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;
using WallowClaims = Wallow.Shared.Kernel.Extensions.ClaimsPrincipalExtensions;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// Checks claim destinations during <see cref="TokenController"/> authorization-code exchange.
/// Identity-token profile, email, and role claims require their corresponding scopes.
/// </summary>
public sealed class TokenControllerTests : IDisposable
{
    private const string OrgId = "11111111-1111-1111-1111-111111111111";
    private const string OrgName = "Acme";
    private const string SessionId = "22222222-2222-2222-2222-222222222222";

    private readonly UserManager<WallowUser> _userManager;
    private readonly TokenController _controller;
    private readonly WallowUser _user;

    public TokenControllerTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _user = WallowUser.Create(
            "Test", "User", "test@example.com", TimeProvider.System);

        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(_user);

        IMembershipRoleResolver membershipRoleResolver = Substitute.For<IMembershipRoleResolver>();
        membershipRoleResolver
            .GetRoleNamesAsync(_user.Id, Guid.Parse(OrgId), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>(["Admin"]);

        IMembershipRepository memberships = Substitute.For<IMembershipRepository>();
        memberships
            .GetAsync(_user.Id, Guid.Parse(OrgId), Arg.Any<CancellationToken>())
            .Returns(Membership.Enroll(
                _user.Id,
                OrganizationId.Create(Guid.Parse(OrgId)),
                Guid.NewGuid(),
                TimeProvider.System));

        _controller = new TokenController(
            _userManager,
            Substitute.For<IOpenIddictApplicationManager>(),
            memberships,
            membershipRoleResolver,
            NullLogger<TokenController>.Instance);
    }

    public void Dispose()
    {
        _controller.Dispose();
        _userManager.Dispose();
    }

    [Fact]
    public async Task Exchange_AuthorizationCode_WithProfileEmailRolesScope_RoutesAllProfileClaimsToIdentityToken()
    {
        SetupAuthorizationCodeExchange("openid", "profile", "email", "roles");

        IActionResult result = await _controller.Exchange();

        ClaimsPrincipal principal = ResultPrincipal(result);


        DestinationsFor(principal, Claims.Email).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.Name).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.GivenName).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.FamilyName).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.Role).Should().Contain(Destinations.IdentityToken);


        DestinationsFor(principal, Claims.Subject).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, "org_id").Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, "org_name").Should().Contain(Destinations.IdentityToken);


        DestinationsFor(principal, Claims.Email).Should().Contain(Destinations.AccessToken);
        DestinationsFor(principal, Claims.Role).Should().Contain(Destinations.AccessToken);
    }

    [Fact]
    public async Task Exchange_AuthorizationCode_WithProfileScopeOnly_DoesNotLeakEmailToIdentityToken()
    {
        SetupAuthorizationCodeExchange("openid", "profile");

        IActionResult result = await _controller.Exchange();

        ClaimsPrincipal principal = ResultPrincipal(result);


        DestinationsFor(principal, Claims.Name).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.GivenName).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.FamilyName).Should().Contain(Destinations.IdentityToken);


        DestinationsFor(principal, Claims.Email).Should().NotContain(Destinations.IdentityToken);
    }

    [Fact]
    public async Task Exchange_AuthorizationCode_WithoutRolesScope_DoesNotRouteRoleToIdentityToken()
    {
        SetupAuthorizationCodeExchange("openid", "profile", "email");

        IActionResult result = await _controller.Exchange();

        ClaimsPrincipal principal = ResultPrincipal(result);


        DestinationsFor(principal, Claims.Email).Should().Contain(Destinations.IdentityToken);


        DestinationsFor(principal, Claims.Role).Should().NotContain(Destinations.IdentityToken);
    }

    [Fact]
    public async Task Exchange_AuthorizationCode_CarriesSidIntoIdentityTokenOnly()
    {
        // Preserve the SID so the relying party can match session logout notifications.
        SetupAuthorizationCodeExchange("openid", "profile");

        IActionResult result = await _controller.Exchange();

        ClaimsPrincipal principal = ResultPrincipal(result);

        principal.GetClaim(WallowClaims.SessionIdClaimType).Should().Be(SessionId);
        DestinationsFor(principal, WallowClaims.SessionIdClaimType)
            .Should().Contain(Destinations.IdentityToken);

        // This API keeps the SID out of access tokens.
        DestinationsFor(principal, WallowClaims.SessionIdClaimType)
            .Should().NotContain(Destinations.AccessToken);
    }

    private void SetupAuthorizationCodeExchange(params string[] scopes)
    {
        OpenIddictRequest request = new() { GrantType = GrantTypes.AuthorizationCode };

        DefaultHttpContext httpContext = new();

        OpenIddictServerTransaction transaction = new() { Request = request };
        httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });

        ClaimsIdentity incoming = new("oidc");
        incoming.SetClaim(Claims.Subject, _user.Id.ToString());
        incoming.SetClaim("org_id", OrgId);
        incoming.SetClaim("org_name", OrgName);
        incoming.SetClaim(WallowClaims.SessionIdClaimType, SessionId);

        ClaimsPrincipal incomingPrincipal = new(incoming);
        incomingPrincipal.SetScopes([.. scopes]);

        AuthenticationTicket ticket = new(
            incomingPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        IAuthenticationService authenticationService = Substitute.For<IAuthenticationService>();
        authenticationService
            .AuthenticateAsync(Arg.Any<HttpContext>(), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
            .Returns(AuthenticateResult.Success(ticket));

        ServiceCollection services = new();
        services.AddSingleton(authenticationService);
        httpContext.RequestServices = services.BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private static ClaimsPrincipal ResultPrincipal(IActionResult result)
    {
        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        return signIn.Principal;
    }

    private static ImmutableArray<string> DestinationsFor(ClaimsPrincipal principal, string claimType)
    {
        Claim claim = principal.Claims.First(c => c.Type == claimType);
        return claim.GetDestinations();
    }
}
