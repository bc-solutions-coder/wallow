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

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// Checks that <see cref="TokenController"/> sets the API resource on sign-in principals
/// for authorization-code and client-credentials exchanges.
/// </summary>
public sealed class TokenControllerAudienceTests : IDisposable
{
    /// <summary>
    /// API resource expected by the issuance and validation configuration.
    /// </summary>
    private const string ApiAudience = "wallow-api";

    private const string OrgId = "11111111-1111-1111-1111-111111111111";
    private const string ServiceAccountClientId = "sa-acme-worker";

    private readonly UserManager<WallowUser> _userManager;
    private readonly TokenController _controller;
    private readonly WallowUser _user;

    public TokenControllerAudienceTests()
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
    public async Task Exchange_AuthorizationCode_ShouldAudienceRestrict_TheIssuedToken()
    {
        ArrangeAuthorizationCodeExchange();

        IActionResult result = await _controller.Exchange();

        ImmutableArray<string> resources = ResultPrincipal(result).GetResources();

        resources.Should().Contain(
            ApiAudience,
            "OpenIddict stamps the aud claim from the resources on the signed-in principal. With " +
            "no resource set, the user access token this exchange returns is accepted by any " +
            "resource server that trusts this issuer, which is the platform-wide authority R24 " +
            "exists to remove");
    }

    [Fact]
    public async Task Exchange_ClientCredentials_ShouldAudienceRestrict_TheIssuedToken()
    {
        ArrangeClientCredentialsExchange();

        IActionResult result = await _controller.Exchange();

        ImmutableArray<string> resources = ResultPrincipal(result).GetResources();

        resources.Should().Contain(
            ApiAudience,
            "service-account tokens need the same audience restriction as user tokens — they are " +
            "the longer-lived of the two and the ones a fork hands to third-party integrations");
    }

    private void ArrangeAuthorizationCodeExchange()
    {
        DefaultHttpContext httpContext = CreateOpenIddictContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.AuthorizationCode
        });

        ClaimsIdentity incoming = new("oidc");
        incoming.SetClaim(Claims.Subject, _user.Id.ToString());
        incoming.SetClaim("org_id", OrgId);

        ClaimsPrincipal incomingPrincipal = new(incoming);
        incomingPrincipal.SetScopes([Scopes.OpenId, Scopes.Profile, Scopes.Email]);

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

    private void ArrangeClientCredentialsExchange()
    {
        DefaultHttpContext httpContext = CreateOpenIddictContext(new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials,
            ClientId = ServiceAccountClientId
        });

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private static DefaultHttpContext CreateOpenIddictContext(OpenIddictRequest request)
    {
        DefaultHttpContext httpContext = new();
        OpenIddictServerTransaction transaction = new() { Request = request };
        httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });
        return httpContext;
    }

    private static ClaimsPrincipal ResultPrincipal(IActionResult result)
    {
        Microsoft.AspNetCore.Mvc.SignInResult signIn =
            result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>().Subject;
        return signIn.Principal;
    }
}
