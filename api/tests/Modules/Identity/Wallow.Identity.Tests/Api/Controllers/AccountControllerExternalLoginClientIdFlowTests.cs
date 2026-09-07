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
/// Checks client-ID propagation through external login. The challenge stores it in
/// <see cref="AuthenticationProperties"/> so the provider callback URL remains unchanged.
/// Redirects to the auth app carry client_id; controller arguments use clientId.
/// </summary>
public class AccountControllerExternalLoginClientIdFlowTests
{
    private const string AuthUrl = "http://localhost:5002";
    private const string ClientId = "client-a";

    /// <summary>
    /// URL allowed only for client-a by the validator stub.
    /// </summary>
    private const string ClientAUrl = "https://a.example.com/callback";

    /// <summary>
    /// Challenge property used to carry the client ID through external login.
    /// </summary>
    private const string ClientIdItemKey = "client_id";

    /// <summary>
    /// Client-ID query fragment expected on redirects to the auth app.
    /// </summary>
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

    /// <summary>
    /// Captured callback URL arguments from ExternalLogin.
    /// </summary>
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
            TimeProvider.System,
            Substitute.For<IEmailChangeRateLimiter>());

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
    /// Allows only the specified URI and client ID pair.
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
    /// Allows all return URLs so these tests can inspect redirect fields.
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
    /// Supplies a properties bag for ExternalLogin to populate.
    /// </summary>
    private void SetupExternalAuthenticationProperties()
    {
        _signInManager
            .ConfigureExternalAuthenticationProperties(Provider, Arg.Any<string>())
            .Returns(_ => new AuthenticationProperties());
    }

    /// <summary>
    /// Stubs external identity information with an optional client-ID property.
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

    /// <summary>
    /// Stubs a linked account with successful external sign-in.
    /// </summary>
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

    /// <summary>
    /// Stubs an unknown account that must accept terms.
    /// </summary>
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
    /// Omit the challenge property when no client ID was supplied.
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
    /// Keep the client ID in challenge properties so it does not alter the provider callback URL.
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

        // Model a callback that receives its client ID only from challenge properties.
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
    /// An explicit client ID must still reach redirect validation.
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
    /// Omit client_id from the accept-terms redirect when no client ID was supplied.
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
    /// Preserve the client ID when redirecting back to accept terms.
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
