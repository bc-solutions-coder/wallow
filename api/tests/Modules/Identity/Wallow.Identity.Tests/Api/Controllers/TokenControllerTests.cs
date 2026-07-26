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
using Wallow.Identity.Domain.Entities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// Unit tests for <see cref="TokenController"/>'s authorization_code / refresh_token token
/// exchange. Regression coverage for Wallow-ho2k: the controller must set the granted scopes
/// on the identity BEFORE computing claim destinations so profile/email/roles claims reach the
/// id_token, and must gate each claim on the correct scope (name/given_name/family_name on
/// 'profile', email on 'email', role on 'roles') so a client granted only 'profile' does not
/// leak email into the id_token.
/// </summary>
public sealed class TokenControllerTests : IDisposable
{
    private const string OrgId = "11111111-1111-1111-1111-111111111111";
    private const string OrgName = "Acme";

    private readonly UserManager<WallowUser> _userManager;
    private readonly TokenController _controller;
    private readonly WallowUser _user;

    public TokenControllerTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _user = WallowUser.Create(
            Guid.NewGuid(), "Test", "User", "test@example.com", TimeProvider.System);

        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(_user);
        _userManager.GetRolesAsync(_user).Returns(new List<string> { "Admin" });

        _controller = new TokenController(_userManager, NullLogger<TokenController>.Instance);
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

        // Standard identity claims must reach BOTH the id_token and the access_token.
        DestinationsFor(principal, Claims.Email).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.Name).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.GivenName).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.FamilyName).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.Role).Should().Contain(Destinations.IdentityToken);

        // Unconditional claims are always in both tokens.
        DestinationsFor(principal, Claims.Subject).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, "org_id").Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, "org_name").Should().Contain(Destinations.IdentityToken);

        // Access token still carries them too.
        DestinationsFor(principal, Claims.Email).Should().Contain(Destinations.AccessToken);
        DestinationsFor(principal, Claims.Role).Should().Contain(Destinations.AccessToken);
    }

    [Fact]
    public async Task Exchange_AuthorizationCode_WithProfileScopeOnly_DoesNotLeakEmailToIdentityToken()
    {
        SetupAuthorizationCodeExchange("openid", "profile");

        IActionResult result = await _controller.Exchange();

        ClaimsPrincipal principal = ResultPrincipal(result);

        // 'profile' releases name/given_name/family_name to the id_token...
        DestinationsFor(principal, Claims.Name).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.GivenName).Should().Contain(Destinations.IdentityToken);
        DestinationsFor(principal, Claims.FamilyName).Should().Contain(Destinations.IdentityToken);

        // ...but NOT email — email requires the 'email' scope (per-claim gating).
        DestinationsFor(principal, Claims.Email).Should().NotContain(Destinations.IdentityToken);
    }

    [Fact]
    public async Task Exchange_AuthorizationCode_WithoutRolesScope_DoesNotRouteRoleToIdentityToken()
    {
        SetupAuthorizationCodeExchange("openid", "profile", "email");

        IActionResult result = await _controller.Exchange();

        ClaimsPrincipal principal = ResultPrincipal(result);

        // email is granted here, so it must reach the id_token...
        DestinationsFor(principal, Claims.Email).Should().Contain(Destinations.IdentityToken);

        // ...but role is NOT ('roles' scope absent).
        DestinationsFor(principal, Claims.Role).Should().NotContain(Destinations.IdentityToken);
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
