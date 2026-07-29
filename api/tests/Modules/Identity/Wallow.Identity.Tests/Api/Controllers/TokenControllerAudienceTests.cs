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
/// Audience restriction on issued access tokens (bead Wallow-pu6a.6.5, guardrail R24 of the SDK
/// review; RFC 9700 §2.3: "access tokens SHOULD be audience-restricted").
///
/// <para>Today no code path anywhere in <c>api/src</c> sets a resource or an audience, so every
/// access token this issuer mints is valid at every resource that trusts the issuer. OpenIddict
/// derives the <c>aud</c> claim from the resources set on the signed-in principal, so both grant
/// types <see cref="TokenController"/> serves — authorization_code/refresh_token and
/// client_credentials — must set the API resource before signing in.</para>
///
/// <para>The matching acceptance half (the validation handler rejecting a token whose audience is
/// not this API) is asserted in <c>Wallow.Architecture.Tests.AccessTokenAudienceTests</c>: the
/// OpenIddict validation options are configured inside a composition root that needs EF, Redis,
/// and certificates, so it cannot be built here.</para>
/// </summary>
public sealed class TokenControllerAudienceTests : IDisposable
{
    /// <summary>
    /// The audience every Wallow access token must carry. Both the issuance side (this file) and
    /// the validation side must agree on this literal; it is the value the fix has to use.
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
            Guid.NewGuid(), "Test", "User", "test@example.com", TimeProvider.System);

        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(_user);
        _userManager.GetRolesAsync(_user).Returns(new List<string> { "Admin" });

        _controller = new TokenController(
            _userManager,
            Substitute.For<IOpenIddictApplicationManager>(),
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
