using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

public class AccountControllerExternalLoginTests
{
    private readonly AccountController _controller;
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly UserManager<WallowUser> _userManager;
    private readonly IRedirectUriValidator _redirectUriValidator;
    private readonly IAuthenticationSchemeProvider _authSchemeProvider;
    private readonly IMfaExemptionChecker _mfaExemptionChecker;
    private readonly IMfaPartialAuthService _mfaPartialAuthService;
    private readonly IOrganizationMfaPolicyService _orgMfaPolicyService;
    private readonly IMessageBus _messageBus;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public AccountControllerExternalLoginTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

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
                ["AuthUrl"] = "http://localhost:5002"
            })
            .Build();

        _redirectUriValidator = Substitute.For<IRedirectUriValidator>();
        _authSchemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        _dataProtectionProvider = Substitute.For<IDataProtectionProvider>();
        _messageBus = Substitute.For<IMessageBus>();
        _mfaExemptionChecker = Substitute.For<IMfaExemptionChecker>();
        _mfaPartialAuthService = Substitute.For<IMfaPartialAuthService>();
        _orgMfaPolicyService = Substitute.For<IOrganizationMfaPolicyService>();

        _controller = new AccountController(
            _signInManager,
            configuration,
            _redirectUriValidator,
            _dataProtectionProvider,
            _authSchemeProvider,
            _messageBus,
            Substitute.For<IClientTenantResolver>(),
            Substitute.For<IOrganizationService>(),
            Substitute.For<IPasswordlessService>(),
            _mfaExemptionChecker,
            Substitute.For<IMfaService>(),
            _mfaPartialAuthService,
            _orgMfaPolicyService,
            Substitute.For<ILogger<AccountController>>(),
            TimeProvider.System);

        // Set up HttpContext with a mock auth service so SignOutAsync works
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
        urlHelper.Action(Arg.Any<UrlActionContext>()).Returns("http://localhost:5000/api/v1/identity/auth/external-login-callback?returnUrl=http://localhost:5002");
        _controller.Url = urlHelper;
    }

    private WallowUser CreateTestUser(bool mfaEnabled = false)
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@example.com", TimeProvider.System);
        if (mfaEnabled)
        {
            typeof(WallowUser).GetProperty(nameof(WallowUser.MfaEnabled))!
                .SetValue(user, true);
        }
        return user;
    }

    private void SetupExternalLoginInfo(string provider = "Google", string email = "test@example.com")
    {
        ClaimsIdentity identity = new(new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim("email_verified", "true")
        });
        ClaimsPrincipal principal = new(identity);
        ExternalLoginInfo info = new(principal, provider, "provider-key-123", provider);

        _signInManager.GetExternalLoginInfoAsync(Arg.Any<string>())
            .Returns(info);
    }

    [Fact]
    public async Task ExternalLogin_WithEmptyProvider_ReturnsBadRequest()
    {
        IActionResult result = await _controller.ExternalLogin("", "http://localhost:5002");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExternalLogin_WithUnregisteredProvider_ReturnsBadRequest()
    {
        _authSchemeProvider.GetSchemeAsync("FakeProvider")
            .Returns((AuthenticationScheme?)null);

        IActionResult result = await _controller.ExternalLogin("FakeProvider", "http://localhost:5002");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExternalLogin_WithInvalidReturnUrl_RedirectsToError()
    {
        _authSchemeProvider.GetSchemeAsync("Google")
            .Returns(new AuthenticationScheme("Google", "Google", typeof(IAuthenticationHandler)));
        _redirectUriValidator.IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await _controller.ExternalLogin("Google", "http://evil.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("error?reason=invalid_redirect_uri");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithoutAcceptedTerms_RedirectsToAcceptTerms()
    {
        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: false,
            returnUrl: "http://localhost:5002");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("accept-terms?error=terms_required");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithNoCookie_RedirectsToSessionExpired()
    {
        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: true,
            returnUrl: "http://localhost:5002");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("error=session_expired");
    }

    [Fact]
    public async Task GetExternalProviders_ReturnsOnlySignInManagerExternalSchemes()
    {
        List<AuthenticationScheme> schemes = new()
        {
            new AuthenticationScheme("TestProvider", "Test Provider", typeof(IAuthenticationHandler))
        };
        _signInManager.GetExternalAuthenticationSchemesAsync()
            .Returns(schemes);

        IActionResult result = await _controller.GetExternalProviders();

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        List<string> providers = okResult.Value.Should().BeOfType<List<string>>().Subject;
        providers.Should().ContainSingle().Which.Should().Be("Test Provider");
    }

    [Fact]
    public async Task GetExternalProviders_ExcludesProvidersNotReturnedBySignInManager()
    {
        _signInManager.GetExternalAuthenticationSchemesAsync()
            .Returns(new List<AuthenticationScheme>());

        IActionResult result = await _controller.GetExternalProviders();

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        List<string> providers = okResult.Value.Should().BeOfType<List<string>>().Subject;
        providers.Should().BeEmpty();
    }

    #region ExternalLoginCallback - MFA Enforcement

    [Fact]
    public async Task ExternalLoginCallback_WhenUserHasMfaEnabled_RedirectsToMfaChallengeAndIssuesPartialCookie()
    {
        WallowUser user = CreateTestUser(mfaEnabled: true);
        SetupExternalLoginInfo("Google", "test@example.com");

        _signInManager.ExternalLoginSignInAsync("Google", "provider-key-123", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _userManager.FindByEmailAsync("test@example.com").Returns(user);
        _redirectUriValidator.IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _mfaExemptionChecker.IsExemptAsync(user, Arg.Any<CancellationToken>()).Returns(false);

        IActionResult result = await _controller.ExternalLoginCallback("http://localhost:5002");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("/mfa/challenge");

        await _mfaPartialAuthService.Received(1).IssuePartialCookieAsync(
            Arg.Is<MfaPartialAuthPayload>(p =>
                p.UserId == user.Id.ToString() &&
                p.Email == "test@example.com" &&
                p.AuthMethod.StartsWith("external:")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginCallback_WhenUserHasMfaDisabledAndOrgDoesNotRequireMfa_RedirectsNormally()
    {
        WallowUser user = CreateTestUser(mfaEnabled: false);
        SetupExternalLoginInfo("Google", "test@example.com");

        _signInManager.ExternalLoginSignInAsync("Google", "provider-key-123", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _userManager.FindByEmailAsync("test@example.com").Returns(user);
        _redirectUriValidator.IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _orgMfaPolicyService.CheckAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new OrgMfaPolicyResult(false, false));

        IActionResult result = await _controller.ExternalLoginCallback("http://localhost:5002");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("http://localhost:5002");
        redirect.Url.Should().NotContain("/mfa/challenge");

        await _mfaPartialAuthService.DidNotReceive().IssuePartialCookieAsync(
            Arg.Any<MfaPartialAuthPayload>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region CompleteExternalRegistration - MFA Org Policy

    [Fact]
    public async Task CompleteExternalRegistration_WhenNewUserInMfaRequiredOrg_SetsMfaGraceDeadline()
    {
        // Set up the cookie with external login state
        IDataProtector protector = Substitute.For<IDataProtector>();
        _dataProtectionProvider.CreateProtector("ExternalLogin").Returns(protector);
        string cookieData = "Google|provider-key-123|newuser@example.com|New|User|true";
        protector.Unprotect(Arg.Any<byte[]>())
            .Returns(System.Text.Encoding.UTF8.GetBytes(cookieData));

        // Mock the cookie to return a value
        _controller.ControllerContext.HttpContext.Request.Headers.Append("Cookie", "ExternalLoginState=encrypted-value");

        // User doesn't exist yet (new registration)
        _userManager.FindByEmailAsync("newuser@example.com").Returns((WallowUser?)null);

        // User creation succeeds
        _userManager.CreateAsync(Arg.Any<WallowUser>())
            .Returns(IdentityResult.Success);
        _userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<WallowUser>())
            .Returns("confirm-token");
        _userManager.ConfirmEmailAsync(Arg.Any<WallowUser>(), "confirm-token")
            .Returns(IdentityResult.Success);
        _userManager.AddLoginAsync(Arg.Any<WallowUser>(), Arg.Any<UserLoginInfo>())
            .Returns(IdentityResult.Success);

        _redirectUriValidator.IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        // Org requires MFA
        _orgMfaPolicyService.CheckAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new OrgMfaPolicyResult(RequiresMfa: true, IsInGracePeriod: true));

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: true,
            returnUrl: "http://localhost:5002");

        // Verify that the newly created user had MfaGraceDeadline set
        await _userManager.Received().UpdateAsync(Arg.Is<WallowUser>(u =>
            u.MfaGraceDeadline != null && u.MfaGraceDeadline > DateTimeOffset.UtcNow));
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
