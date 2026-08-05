using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// Wallow-9jab scoped every AccountController redirect check to a client id, but nothing on the
/// external-login return path SUPPLIES one, so in production those endpoints still see
/// <c>clientId = null</c> and fall back to the AuthUrl-only origin set. These tests pin the onward
/// plumbing that makes the per-client scoping real:
/// <list type="number">
/// <item>ExternalLogin stashes the requesting client id in the challenge's
/// <see cref="AuthenticationProperties"/> — which round-trip through the provider's OAuth state —
/// rather than appending it to the callback URL. The callback URL is the <c>redirect_uri</c>
/// presented to the third-party IdP and Google requires an EXACT registered match, so a query
/// param there would break every configured provider.</item>
/// <item>ExternalLoginCallback recovers that stashed id and validates the returnUrl against it.</item>
/// <item>Every hand-off back to the auth app (accept-terms, mfa/challenge, the terms_required
/// bounce) carries <c>client_id</c> so the wallow-auth screens can echo it to the endpoint that
/// finishes the flow.</item>
/// </list>
/// The auth-app-facing redirects use the snake_case <c>client_id</c> spelling AuthorizationController
/// already uses on its login redirect; the API endpoints keep binding the camelCase <c>clientId</c>
/// query parameter they gained in Wallow-9jab.
/// </summary>
public class AccountControllerExternalLoginClientIdFlowTests
{
    private const string AuthUrl = "http://localhost:5002";
    private const string ClientId = "client-a";

    /// <summary>An origin registered by client-a only.</summary>
    private const string ClientAUrl = "https://a.example.com/callback";

    /// <summary>
    /// The key the challenge stashes the client id under and the callback reads it back from. The
    /// value must survive the provider round trip in the OAuth state, not in the redirect_uri.
    /// </summary>
    private const string ClientIdItemKey = "client_id";

    /// <summary>The spelling the auth app's screens receive on their query string.</summary>
    private const string ClientIdQueryParam = "client_id=client-a";

    private const string Provider = "Google";
    private const string ProviderKey = "provider-key-123";
    private const string TestEmail = "external-client-id@test.com";

    private readonly AccountController _controller;
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly UserManager<WallowUser> _userManager;
    private readonly IRedirectUriValidator _redirectUriValidator;
    private readonly IAuthenticationSchemeProvider _authSchemeProvider;
    private readonly IMfaExemptionChecker _mfaExemptionChecker;
    private readonly IOrganizationMfaPolicyService _orgMfaPolicyService;

    /// <summary>The context ExternalLogin handed IUrlHelper when it built the provider callback URL.</summary>
    private UrlActionContext? _callbackUrlContext;

    public AccountControllerExternalLoginClientIdFlowTests()
    {
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);

        _signInManager = Substitute.For<SignInManager<WallowUser>>(
            _userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<WallowUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<ILogger<SignInManager<WallowUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<WallowUser>>());

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthUrl"] = AuthUrl
            })
            .Build();

        _redirectUriValidator = Substitute.For<IRedirectUriValidator>();
        _authSchemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        _mfaExemptionChecker = Substitute.For<IMfaExemptionChecker>();
        _orgMfaPolicyService = Substitute.For<IOrganizationMfaPolicyService>();

        _controller = new AccountController(
            _signInManager,
            configuration,
            _redirectUriValidator,
            new EphemeralDataProtectionProvider(),
            _authSchemeProvider,
            Substitute.For<IMessageBus>(),
            Substitute.For<IClientTenantResolver>(),
            Substitute.For<IPasswordlessService>(),
            _mfaExemptionChecker,
            Substitute.For<IMfaService>(),
            Substitute.For<IMfaPartialAuthService>(),
            _orgMfaPolicyService,
            Substitute.For<IMfaLockoutService>(),
            Substitute.For<IConnectionMultiplexer>(),
            Substitute.For<ILogger<AccountController>>(),
            TimeProvider.System);

        DefaultHttpContext httpContext = new();
        IAuthenticationService authService = Substitute.For<IAuthenticationService>();
        authService.SignOutAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<AuthenticationProperties>())
            .Returns(Task.CompletedTask);
        httpContext.RequestServices = new TestServiceProvider(authService);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        IUrlHelper urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.Action(Arg.Do<UrlActionContext>(context => _callbackUrlContext = context))
            .Returns("http://localhost:5001/v1/identity/auth/external-login-callback");
        _controller.Url = urlHelper;
    }

    /// <summary>
    /// Models the per-client allow list: <paramref name="uri"/> validates only when the validator is
    /// told which client is asking. A call that omits the client id — today's behaviour on this path
    /// — is refused, so an endpoint that fails to recover the stashed id cannot pass.
    /// </summary>
    private void AllowOnlyForClient(string uri, string clientId)
    {
        _redirectUriValidator
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _redirectUriValidator
            .IsAllowedAsync(uri, clientId, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    /// <summary>
    /// Takes redirect validation out of the picture, so the redirect-shape tests fail only when the
    /// client id is missing from the hand-off and never because the returnUrl was rejected.
    /// </summary>
    private void AllowEveryReturnUrl()
    {
        _redirectUriValidator
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private void SetupRegisteredProvider()
    {
        _authSchemeProvider.GetSchemeAsync(Provider)
            .Returns(new AuthenticationScheme(Provider, Provider, typeof(IAuthenticationHandler)));
    }

    /// <summary>
    /// Hands ExternalLogin a real properties bag to populate. The substituted SignInManager returns
    /// null by default, which would leave nowhere to stash the client id.
    /// </summary>
    private void SetupExternalAuthenticationProperties()
    {
        _signInManager
            .ConfigureExternalAuthenticationProperties(Provider, Arg.Any<string>())
            .Returns(_ => new AuthenticationProperties());
    }

    /// <summary>
    /// The identity the provider hands back, optionally carrying the client id the challenge stashed.
    /// A null <paramref name="stashedClientId"/> models a flow started before this plumbing existed.
    /// </summary>
    private ExternalLoginInfo SetupExternalLoginInfo(string? stashedClientId)
    {
        ClaimsIdentity identity = new(new[]
        {
            new Claim(ClaimTypes.Email, TestEmail),
            new Claim("email_verified", "true")
        });

        ExternalLoginInfo info = new(new ClaimsPrincipal(identity), Provider, ProviderKey, Provider);

        if (stashedClientId is not null)
        {
            info.AuthenticationProperties = new AuthenticationProperties(
                new Dictionary<string, string?> { [ClientIdItemKey] = stashedClientId });
        }

        _signInManager.GetExternalLoginInfoAsync(Arg.Any<string>()).Returns(info);
        return info;
    }

    private static WallowUser CreateUser(bool mfaEnabled)
    {
        WallowUser user = WallowUser.Create("Test", "User", TestEmail, TimeProvider.System);

        if (mfaEnabled)
        {
            typeof(WallowUser).GetProperty(nameof(WallowUser.MfaEnabled))!.SetValue(user, true);
        }

        return user;
    }

    /// <summary>Path A: the external login is already linked and the sign-in succeeds.</summary>
    private void SetupLinkedAccount(string? stashedClientId, bool mfaEnabled = false, bool orgRequiresMfa = false)
    {
        SetupExternalLoginInfo(stashedClientId);

        WallowUser user = CreateUser(mfaEnabled);
        _signInManager.ExternalLoginSignInAsync(Provider, ProviderKey, false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _mfaExemptionChecker.IsExemptAsync(user, Arg.Any<CancellationToken>()).Returns(false);
        _orgMfaPolicyService.CheckAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new OrgMfaPolicyResult(orgRequiresMfa, IsInGracePeriod: false));
    }

    /// <summary>Path C: nobody owns this email yet, so the flow gates on accept-terms.</summary>
    private void SetupUnknownAccount(string? stashedClientId)
    {
        SetupExternalLoginInfo(stashedClientId);

        _signInManager.ExternalLoginSignInAsync(Provider, ProviderKey, false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        _userManager.FindByEmailAsync(TestEmail).Returns((WallowUser?)null);
    }

    #region ExternalLogin stashes the client id in the challenge

    [Fact]
    public async Task ExternalLogin_StashesRequestClientIdInChallengeProperties()
    {
        SetupRegisteredProvider();
        SetupExternalAuthenticationProperties();
        AllowEveryReturnUrl();

        IActionResult result = await _controller.ExternalLogin(Provider, ClientAUrl, ClientId);

        ChallengeResult challenge = result.Should().BeOfType<ChallengeResult>().Subject;
        challenge.Properties.Should().NotBeNull();
        challenge.Properties!.Items.Should().ContainKey(ClientIdItemKey)
            .WhoseValue.Should().Be(ClientId);
    }

    /// <summary>
    /// Regression guard: a flow started without a client id must not stash an empty one, or the
    /// callback would validate against a client that does not exist and fail closed to AuthUrl.
    /// </summary>
    [Fact]
    public async Task ExternalLogin_WithoutClientId_StashesNothingInChallengeProperties()
    {
        SetupRegisteredProvider();
        SetupExternalAuthenticationProperties();
        AllowEveryReturnUrl();

        IActionResult result = await _controller.ExternalLogin(Provider, ClientAUrl, clientId: null);

        ChallengeResult challenge = result.Should().BeOfType<ChallengeResult>().Subject;
        challenge.Properties.Should().NotBeNull();
        challenge.Properties!.Items.Should().NotContainKey(ClientIdItemKey);
    }

    /// <summary>
    /// The design pin, and the reason this work was split out of Wallow-9jab: the callback URL is the
    /// <c>redirect_uri</c> sent to the third-party IdP, and Google matches it EXACTLY against the
    /// registered value. The client id must ride the challenge properties instead, so this must stay
    /// green — a green phase that "threads" the id by appending it here breaks every provider.
    /// </summary>
    [Fact]
    public async Task ExternalLogin_DoesNotAddClientIdToTheProviderCallbackUrl()
    {
        SetupRegisteredProvider();
        SetupExternalAuthenticationProperties();
        AllowEveryReturnUrl();

        await _controller.ExternalLogin(Provider, ClientAUrl, ClientId);

        _callbackUrlContext.Should().NotBeNull();
        RouteValueDictionary routeValues = new(_callbackUrlContext!.Values);
        routeValues.Should().ContainKey("returnUrl");
        routeValues.Should().NotContainKey("clientId");
        routeValues.Should().NotContainKey(ClientIdItemKey);
    }

    #endregion

    #region ExternalLoginCallback recovers the stashed client id

    [Fact]
    public async Task ExternalLoginCallback_RecoversClientIdStashedByTheChallenge()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);
        SetupLinkedAccount(stashedClientId: ClientId);

        // The provider drove the browser here; there is no clientId on the query string, only the
        // one the challenge stashed.
        IActionResult result = await _controller.ExternalLoginCallback(ClientAUrl, clientId: null);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(ClientAUrl);
    }

    [Fact]
    public async Task ExternalLoginCallback_PassesTheStashedClientIdToTheRedirectValidator()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);
        SetupLinkedAccount(stashedClientId: ClientId);

        await _controller.ExternalLoginCallback(ClientAUrl, clientId: null);

        await _redirectUriValidator.Received(1)
            .IsAllowedAsync(ClientAUrl, ClientId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Regression guard for Wallow-9jab: an explicit query parameter still wins where one is present
    /// (the auth app's own hand-offs), so recovering from the stash must not displace it.
    /// </summary>
    [Fact]
    public async Task ExternalLoginCallback_WithExplicitClientId_StillValidatesAgainstIt()
    {
        AllowOnlyForClient(ClientAUrl, ClientId);
        SetupLinkedAccount(stashedClientId: null);

        IActionResult result = await _controller.ExternalLoginCallback(ClientAUrl, ClientId);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(ClientAUrl);
    }

    #endregion

    #region The hand-offs back to the auth app carry client_id

    [Fact]
    public async Task ExternalLoginCallback_NewUser_AcceptTermsRedirectCarriesClientId()
    {
        AllowEveryReturnUrl();
        SetupUnknownAccount(stashedClientId: ClientId);

        IActionResult result = await _controller.ExternalLoginCallback(ClientAUrl, clientId: null);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("/accept-terms?");
        redirect.Url.Should().Contain(ClientIdQueryParam);
    }

    /// <summary>
    /// Regression guard: a flow with no client id must not grow an empty <c>client_id=</c>, which the
    /// accept-terms screen would echo back and the endpoint would treat as an unknown client.
    /// </summary>
    [Fact]
    public async Task ExternalLoginCallback_NewUserWithoutClientId_AcceptTermsRedirectCarriesNone()
    {
        AllowEveryReturnUrl();
        SetupUnknownAccount(stashedClientId: null);

        IActionResult result = await _controller.ExternalLoginCallback(ClientAUrl, clientId: null);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("/accept-terms?");
        redirect.Url.Should().NotContain(ClientIdItemKey);
    }

    [Fact]
    public async Task ExternalLoginCallback_UserWithMfaEnabled_MfaChallengeRedirectCarriesClientId()
    {
        AllowEveryReturnUrl();
        SetupLinkedAccount(stashedClientId: ClientId, mfaEnabled: true);

        IActionResult result = await _controller.ExternalLoginCallback(ClientAUrl, clientId: null);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("/mfa/challenge?");
        redirect.Url.Should().Contain(ClientIdQueryParam);
    }

    [Fact]
    public async Task ExternalLoginCallback_OrgRequiresMfaOutsideGrace_MfaChallengeRedirectCarriesClientId()
    {
        AllowEveryReturnUrl();
        SetupLinkedAccount(stashedClientId: ClientId, mfaEnabled: false, orgRequiresMfa: true);

        IActionResult result = await _controller.ExternalLoginCallback(ClientAUrl, clientId: null);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("/mfa/challenge?");
        redirect.Url.Should().Contain(ClientIdQueryParam);
    }

    /// <summary>
    /// The terms_required bounce sends the user back to the accept-terms screen to try again. Losing
    /// the client id there would silently downgrade the retry to the AuthUrl-only origin set.
    /// </summary>
    [Fact]
    public async Task CompleteExternalRegistration_TermsRequiredBounce_CarriesClientId()
    {
        AllowEveryReturnUrl();

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: false, returnUrl: ClientAUrl, clientId: ClientId);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("accept-terms?error=terms_required");
        redirect.Url.Should().Contain(ClientIdQueryParam);
    }

    #endregion

    /// <summary>
    /// Minimal service provider so HttpContext.SignOutAsync resolves IAuthenticationService.
    /// </summary>
    private sealed class TestServiceProvider(IAuthenticationService authenticationService) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IAuthenticationService))
            {
                return authenticationService;
            }

            return null;
        }
    }
}
