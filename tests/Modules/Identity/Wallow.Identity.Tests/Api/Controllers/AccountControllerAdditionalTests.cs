using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

public class AccountControllerAdditionalTests
{
    private readonly AccountController _controller;
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly UserManager<WallowUser> _userManager;
    private readonly IRedirectUriValidator _redirectUriValidator;
    private readonly EphemeralDataProtectionProvider _dataProtectionProvider;
    private readonly IAuthenticationSchemeProvider _authSchemeProvider;
    private readonly IMessageBus _messageBus;
    private readonly IClientTenantResolver _clientTenantResolver;
    private readonly IPasswordlessService _passwordlessService;

    public AccountControllerAdditionalTests()
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
                ["AuthUrl"] = "http://localhost:5002"
            })
            .Build();

        _redirectUriValidator = Substitute.For<IRedirectUriValidator>();
        _dataProtectionProvider = new EphemeralDataProtectionProvider();
        _authSchemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        _messageBus = Substitute.For<IMessageBus>();
        _clientTenantResolver = Substitute.For<IClientTenantResolver>();
        _passwordlessService = Substitute.For<IPasswordlessService>();

        _controller = new AccountController(
            _signInManager,
            configuration,
            _redirectUriValidator,
            _dataProtectionProvider,
            _authSchemeProvider,
            _messageBus,
            _clientTenantResolver,
            Substitute.For<IOrganizationService>(),
            _passwordlessService,
            Substitute.For<IMfaExemptionChecker>(),
            Substitute.For<IMfaService>(),
            Substitute.For<IMfaPartialAuthService>(),
            Substitute.For<IOrganizationMfaPolicyService>(),
            Substitute.For<ILogger<AccountController>>(),
            TimeProvider.System);

        DefaultHttpContext httpContext = CreateHttpContextWithAuth();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        IUrlHelper urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.Action(Arg.Any<UrlActionContext>())
            .Returns("http://localhost:5000/callback?returnUrl=test");
        urlHelper.IsLocalUrl(Arg.Any<string>()).Returns(callInfo =>
        {
            string? url = callInfo.Arg<string>();
            return url != null && url.StartsWith('/');
        });
        _controller.Url = urlHelper;
    }

    private static DefaultHttpContext CreateHttpContextWithAuth()
    {
        IAuthenticationService authService = Substitute.For<IAuthenticationService>();
        authService.SignOutAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<AuthenticationProperties>())
            .Returns(Task.CompletedTask);
        authService.SignInAsync(Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<AuthenticationProperties>())
            .Returns(Task.CompletedTask);

        ServiceCollection services = new();
        services.AddSingleton(authService);
        DefaultHttpContext httpContext = new()
        {
            RequestServices = services.BuildServiceProvider()
        };
        return httpContext;
    }

    #region Register - DuplicateUserName

    [Fact]
    public async Task Register_WithDuplicateUserName_ReturnsBadRequestWithEmailTaken()
    {
        _userManager.CreateAsync(Arg.Any<WallowUser>(), "Password1!")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName", Description = "Username taken" }));

        IActionResult result = await _controller.Register(
            new AccountRegisterRequest("test@test.com", "Password1!", "Password1!"));

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("email_taken");
    }

    [Fact]
    public async Task Register_WithOtherError_ReturnsBadRequestWithDescription()
    {
        _userManager.CreateAsync(Arg.Any<WallowUser>(), "weak")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "Password too short" }));

        IActionResult result = await _controller.Register(
            new AccountRegisterRequest("test@test.com", "weak", "weak"));

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("Password too short");
    }

    #endregion

    #region ForgotPassword - Unconfirmed email

    [Fact]
    public async Task ForgotPassword_WithUnconfirmedEmail_ReturnsOkWithoutGeneratingToken()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(false);

        IActionResult result = await _controller.ForgotPassword(
            new AccountForgotPasswordRequest("test@test.com"));

        result.Should().BeOfType<OkObjectResult>();
        await _userManager.DidNotReceive().GeneratePasswordResetTokenAsync(Arg.Any<WallowUser>());
    }

    #endregion

    #region ExchangeTicket - Valid ticket

    [Fact]
    public async Task ExchangeTicket_WithValidTicketAndLocalReturnUrl_RedirectsToReturnUrl()
    {
        // Use the real EphemeralDataProtectionProvider to create a valid ticket via Login
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, "password", true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        IActionResult loginResult = await _controller.Login(new AccountLoginRequest("test@test.com", "password", false), CancellationToken.None);

        OkObjectResult ok = loginResult.Should().BeOfType<OkObjectResult>().Subject;
        string loginJson = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(loginJson);
        string ticket = doc.RootElement.GetProperty("signInTicket").GetString()!;

        IActionResult result = await _controller.ExchangeTicket(ticket, "/dashboard");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("/dashboard");
    }

    [Fact]
    public async Task ExchangeTicket_WithValidTicketAndNoReturnUrl_RedirectsToAuthUrl()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, "password", true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        IActionResult loginResult = await _controller.Login(new AccountLoginRequest("test@test.com", "password", true), CancellationToken.None);

        OkObjectResult ok = loginResult.Should().BeOfType<OkObjectResult>().Subject;
        string loginJson = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(loginJson);
        string ticket = doc.RootElement.GetProperty("signInTicket").GetString()!;

        IActionResult result = await _controller.ExchangeTicket(ticket, null);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("http://localhost:5002");
    }

    [Fact]
    public async Task ExchangeTicket_WithValidTicketButUserNotFound_ReturnsBadRequest()
    {
        WallowUser loginUser = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(loginUser);
        _signInManager.CheckPasswordSignInAsync(loginUser, "password", true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        IActionResult loginResult = await _controller.Login(new AccountLoginRequest("test@test.com", "password", false), CancellationToken.None);

        OkObjectResult ok = loginResult.Should().BeOfType<OkObjectResult>().Subject;
        string loginJson = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(loginJson);
        string ticket = doc.RootElement.GetProperty("signInTicket").GetString()!;

        // Now user is deleted
        _userManager.FindByEmailAsync("test@test.com").Returns((WallowUser?)null);

        IActionResult result = await _controller.ExchangeTicket(ticket, null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExchangeTicket_WithNonLocalReturnUrl_RedirectsToAuthUrl()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, "password", true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        IActionResult loginResult = await _controller.Login(new AccountLoginRequest("test@test.com", "password", false), CancellationToken.None);

        OkObjectResult ok = loginResult.Should().BeOfType<OkObjectResult>().Subject;
        string loginJson = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(loginJson);
        string ticket = doc.RootElement.GetProperty("signInTicket").GetString()!;

        IActionResult result = await _controller.ExchangeTicket(ticket, "http://evil.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("http://localhost:5002");
    }

    [Fact]
    public async Task ExchangeTicket_WithFormatException_ReturnsBadRequest()
    {
        IActionResult result = await _controller.ExchangeTicket("not-base64-!@#$", null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region ExternalLogin

    [Fact]
    public async Task ExternalLogin_WithValidProviderAndReturnUrl_ReturnsChallengeResult()
    {
        _authSchemeProvider.GetSchemeAsync("Google")
            .Returns(new AuthenticationScheme("Google", "Google", typeof(IAuthenticationHandler)));
        _redirectUriValidator.IsAllowedAsync("http://app.test.com", Arg.Any<CancellationToken>())
            .Returns(true);
        _signInManager.ConfigureExternalAuthenticationProperties("Google", Arg.Any<string>())
            .Returns(new AuthenticationProperties());

        IActionResult result = await _controller.ExternalLogin("Google", "http://app.test.com");

        result.Should().BeOfType<ChallengeResult>();
    }

    [Fact]
    public async Task ExternalLogin_WithEmptyReturnUrl_RedirectsToError()
    {
        _authSchemeProvider.GetSchemeAsync("Google")
            .Returns(new AuthenticationScheme("Google", "Google", typeof(IAuthenticationHandler)));

        IActionResult result = await _controller.ExternalLogin("Google", "");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("error?reason=invalid_redirect_uri");
    }

    #endregion

    #region ExternalLoginCallback

    [Fact]
    public async Task ExternalLoginCallback_WithNoExternalLoginInfo_RedirectsToLoginError()
    {
        _signInManager.GetExternalLoginInfoAsync(Arg.Any<string>())
            .Returns((ExternalLoginInfo?)null);

        IActionResult result = await _controller.ExternalLoginCallback("http://localhost:5002");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("login?error=external_login_failed");
    }

    [Fact]
    public async Task ExternalLoginCallback_WithExistingLinkedAccount_RedirectsToReturnUrl()
    {
        ClaimsIdentity claimsIdentity = new([new Claim(ClaimTypes.Email, "test@test.com")]);
        ClaimsPrincipal principal = new(claimsIdentity);
        ExternalLoginInfo loginInfo = new(principal, "Google", "key-123", "Google");
        _signInManager.GetExternalLoginInfoAsync(Arg.Any<string>()).Returns(loginInfo);
        _signInManager.ExternalLoginSignInAsync("Google", "key-123", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _redirectUriValidator.IsAllowedAsync("http://app.test.com", Arg.Any<CancellationToken>())
            .Returns(true);

        IActionResult result = await _controller.ExternalLoginCallback("http://app.test.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("http://app.test.com");
    }

    [Fact]
    public async Task ExternalLoginCallback_WithNoEmailClaim_RedirectsToLoginError()
    {
        ClaimsIdentity claimsIdentity = new();
        ClaimsPrincipal principal = new(claimsIdentity);
        ExternalLoginInfo loginInfo = new(principal, "Google", "key-123", "Google");
        _signInManager.GetExternalLoginInfoAsync(Arg.Any<string>()).Returns(loginInfo);
        _signInManager.ExternalLoginSignInAsync("Google", "key-123", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        IActionResult result = await _controller.ExternalLoginCallback("http://app.test.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("login?error=external_login_failed");
    }

    [Fact]
    public async Task ExternalLoginCallback_WithExistingUserAndVerifiedEmail_LinksAndRedirects()
    {
        ClaimsIdentity claimsIdentity = new([
            new Claim(ClaimTypes.Email, "existing@test.com"),
            new Claim("email_verified", "true")
        ]);
        ClaimsPrincipal principal = new(claimsIdentity);
        ExternalLoginInfo loginInfo = new(principal, "Google", "key-123", "Google");
        _signInManager.GetExternalLoginInfoAsync(Arg.Any<string>()).Returns(loginInfo);
        _signInManager.ExternalLoginSignInAsync("Google", "key-123", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        _redirectUriValidator.IsAllowedAsync("http://app.test.com", Arg.Any<CancellationToken>())
            .Returns(true);

        WallowUser existingUser = WallowUser.Create(Guid.Empty, "Test", "User", "existing@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("existing@test.com").Returns(existingUser);
        _userManager.AddLoginAsync(existingUser, loginInfo).Returns(IdentityResult.Success);

        IActionResult result = await _controller.ExternalLoginCallback("http://app.test.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("http://app.test.com");
    }

    [Fact]
    public async Task ExternalLoginCallback_WithExistingUserButLinkFails_RedirectsToError()
    {
        ClaimsIdentity claimsIdentity = new([
            new Claim(ClaimTypes.Email, "existing@test.com"),
            new Claim("email_verified", "true")
        ]);
        ClaimsPrincipal principal = new(claimsIdentity);
        ExternalLoginInfo loginInfo = new(principal, "Google", "key-123", "Google");
        _signInManager.GetExternalLoginInfoAsync(Arg.Any<string>()).Returns(loginInfo);
        _signInManager.ExternalLoginSignInAsync("Google", "key-123", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        WallowUser existingUser = WallowUser.Create(Guid.Empty, "Test", "User", "existing@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("existing@test.com").Returns(existingUser);
        _userManager.AddLoginAsync(existingUser, loginInfo)
            .Returns(IdentityResult.Failed(new IdentityError { Code = "Error" }));

        IActionResult result = await _controller.ExternalLoginCallback("http://app.test.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("login?error=external_login_failed");
    }

    [Fact]
    public async Task ExternalLoginCallback_WithNewUser_SetsCookieAndRedirectsToAcceptTerms()
    {
        ClaimsIdentity claimsIdentity = new([
            new Claim(ClaimTypes.Email, "new@test.com"),
            new Claim(ClaimTypes.GivenName, "Jane"),
            new Claim(ClaimTypes.Surname, "Doe"),
            new Claim("email_verified", "true")
        ]);
        ClaimsPrincipal principal = new(claimsIdentity);
        ExternalLoginInfo loginInfo = new(principal, "Google", "key-123", "Google");
        _signInManager.GetExternalLoginInfoAsync(Arg.Any<string>()).Returns(loginInfo);
        _signInManager.ExternalLoginSignInAsync("Google", "key-123", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        _userManager.FindByEmailAsync("new@test.com").Returns((WallowUser?)null);

        IActionResult result = await _controller.ExternalLoginCallback("http://app.test.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("accept-terms");
        redirect.Url.Should().Contain("email=");
    }

    #endregion

    #region CompleteExternalRegistration

    [Fact]
    public async Task CompleteExternalRegistration_WithValidReturnUrlValidation_UsesReturnUrl()
    {
        _redirectUriValidator.IsAllowedAsync("http://app.test.com", Arg.Any<CancellationToken>())
            .Returns(true);

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: false,
            returnUrl: "http://app.test.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("accept-terms?error=terms_required");
        redirect.Url.Should().Contain(Uri.EscapeDataString("http://app.test.com"));
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithInvalidReturnUrl_FallsBackToAuthUrl()
    {
        _redirectUriValidator.IsAllowedAsync("http://evil.com", Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: false,
            returnUrl: "http://evil.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("accept-terms?error=terms_required");
        redirect.Url.Should().Contain(Uri.EscapeDataString("http://localhost:5002"));
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithEmptyReturnUrl_FallsBackToAuthUrl()
    {
        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: false,
            returnUrl: "");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("accept-terms?error=terms_required");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithAcceptedTermsButNoCookie_RedirectsToSessionExpired()
    {
        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: true,
            returnUrl: "http://localhost:5002");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("error=session_expired");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithCorruptedCookie_RedirectsToSessionExpired()
    {
        // Set up a cookie with value that will fail decryption
        DefaultHttpContext httpContext = CreateHttpContextWithAuth();
        httpContext.Request.Headers.Append("Cookie", "ExternalLoginState=corrupted-value");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: true,
            returnUrl: "http://localhost:5002");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("error=session_expired");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithShortCookieParts_RedirectsToSessionExpired()
    {
        // Create a valid encrypted cookie but with too few pipe-separated parts
        IDataProtector protector = _dataProtectionProvider.CreateProtector("ExternalLogin");
        string shortData = protector.Protect("only|three|parts");

        DefaultHttpContext httpContext = CreateHttpContextWithAuth();
        httpContext.Request.Headers.Append("Cookie", $"ExternalLoginState={Uri.EscapeDataString(shortData)}");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: true,
            returnUrl: "http://localhost:5002");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("error=session_expired");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithValidCookieAndExistingUser_LinksAndRedirects()
    {
        IDataProtector protector = _dataProtectionProvider.CreateProtector("ExternalLogin");
        string cookieValue = protector.Protect("Google|key-123|existing@test.com|Jane|Doe|true");

        DefaultHttpContext httpContext = CreateHttpContextWithAuth();
        httpContext.Request.Headers.Append("Cookie", $"ExternalLoginState={Uri.EscapeDataString(cookieValue)}");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _redirectUriValidator.IsAllowedAsync("http://app.test.com", Arg.Any<CancellationToken>())
            .Returns(true);

        WallowUser existingUser = WallowUser.Create(Guid.Empty, "Jane", "Doe", "existing@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("existing@test.com").Returns(existingUser);
        _userManager.AddLoginAsync(existingUser, Arg.Any<UserLoginInfo>()).Returns(IdentityResult.Success);

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: true,
            returnUrl: "http://app.test.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("http://app.test.com");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithValidCookieAndNewUser_CreatesUserAndRedirects()
    {
        IDataProtector protector = _dataProtectionProvider.CreateProtector("ExternalLogin");
        string cookieValue = protector.Protect("Google|key-123|new@test.com|Jane|Doe|true");

        DefaultHttpContext httpContext = CreateHttpContextWithAuth();
        httpContext.Request.Headers.Append("Cookie", $"ExternalLoginState={Uri.EscapeDataString(cookieValue)}");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _redirectUriValidator.IsAllowedAsync("http://app.test.com", Arg.Any<CancellationToken>())
            .Returns(true);
        _userManager.FindByEmailAsync("new@test.com").Returns((WallowUser?)null);
        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);
        _userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<WallowUser>()).Returns("token");
        _userManager.ConfirmEmailAsync(Arg.Any<WallowUser>(), "token").Returns(IdentityResult.Success);
        _userManager.AddLoginAsync(Arg.Any<WallowUser>(), Arg.Any<UserLoginInfo>()).Returns(IdentityResult.Success);

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: true,
            returnUrl: "http://app.test.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("http://app.test.com");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithNewUserCreateFails_RedirectsToError()
    {
        IDataProtector protector = _dataProtectionProvider.CreateProtector("ExternalLogin");
        string cookieValue = protector.Protect("Google|key-123|new@test.com|Jane|Doe|false");

        DefaultHttpContext httpContext = CreateHttpContextWithAuth();
        httpContext.Request.Headers.Append("Cookie", $"ExternalLoginState={Uri.EscapeDataString(cookieValue)}");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _userManager.FindByEmailAsync("new@test.com").Returns((WallowUser?)null);
        _userManager.CreateAsync(Arg.Any<WallowUser>())
            .Returns(IdentityResult.Failed(new IdentityError { Code = "Error", Description = "Create failed" }));

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: true,
            returnUrl: "http://app.test.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("login?error=external_login_failed");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithNewUserAddLoginFails_RedirectsToError()
    {
        IDataProtector protector = _dataProtectionProvider.CreateProtector("ExternalLogin");
        string cookieValue = protector.Protect("Google|key-123|new@test.com|Jane|Doe|false");

        DefaultHttpContext httpContext = CreateHttpContextWithAuth();
        httpContext.Request.Headers.Append("Cookie", $"ExternalLoginState={Uri.EscapeDataString(cookieValue)}");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _userManager.FindByEmailAsync("new@test.com").Returns((WallowUser?)null);
        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);
        _userManager.AddLoginAsync(Arg.Any<WallowUser>(), Arg.Any<UserLoginInfo>())
            .Returns(IdentityResult.Failed(new IdentityError { Code = "Error" }));

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: true,
            returnUrl: "http://app.test.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("login?error=external_login_failed");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithUnverifiedEmail_SendsVerificationEmail()
    {
        IDataProtector protector = _dataProtectionProvider.CreateProtector("ExternalLogin");
        string cookieValue = protector.Protect("Google|key-123|new@test.com|Jane|Doe|false");

        DefaultHttpContext httpContext = CreateHttpContextWithAuth();
        httpContext.Request.Headers.Append("Cookie", $"ExternalLoginState={Uri.EscapeDataString(cookieValue)}");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _redirectUriValidator.IsAllowedAsync("http://app.test.com", Arg.Any<CancellationToken>())
            .Returns(true);
        _userManager.FindByEmailAsync("new@test.com").Returns((WallowUser?)null);
        _userManager.CreateAsync(Arg.Any<WallowUser>()).Returns(IdentityResult.Success);
        _userManager.AddLoginAsync(Arg.Any<WallowUser>(), Arg.Any<UserLoginInfo>()).Returns(IdentityResult.Success);
        _userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<WallowUser>()).Returns("verify-token");

        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: true,
            returnUrl: "http://app.test.com");

        result.Should().BeOfType<RedirectResult>();
        await _userManager.Received(1).GenerateEmailConfirmationTokenAsync(Arg.Any<WallowUser>());
        await _userManager.DidNotReceive().ConfirmEmailAsync(Arg.Any<WallowUser>(), Arg.Any<string>());
    }

    #endregion

    #region GetExternalProviders - DisplayName fallback

    [Fact]
    public async Task GetExternalProviders_WithNoDisplayName_UsesSchemeNameAsFallback()
    {
        List<AuthenticationScheme> schemes =
        [
            new AuthenticationScheme("google-oidc", null, typeof(IAuthenticationHandler))
        ];
        _signInManager.GetExternalAuthenticationSchemesAsync().Returns(schemes);

        IActionResult result = await _controller.GetExternalProviders();

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        List<string> providers = okResult.Value.Should().BeOfType<List<string>>().Subject;
        providers.Should().ContainSingle().Which.Should().Be("google-oidc");
    }

    #endregion

    #region ForgotPassword - Event publishing

    [Fact]
    public async Task ForgotPassword_UserNotFound_ReturnsOkToPreventEnumeration()
    {
        _userManager.FindByEmailAsync("ghost@test.com").Returns((WallowUser?)null);

        IActionResult result = await _controller.ForgotPassword(
            new AccountForgotPasswordRequest("ghost@test.com"));

        result.Should().BeOfType<OkObjectResult>();
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<PasswordResetRequestedEvent>());
    }

    [Fact]
    public async Task ForgotPassword_UserFoundWithConfirmedEmail_PublishesPasswordResetEvent()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "confirmed@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("confirmed@test.com").Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token-123");

        IActionResult result = await _controller.ForgotPassword(
            new AccountForgotPasswordRequest("confirmed@test.com"));

        result.Should().BeOfType<OkObjectResult>();
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<PasswordResetRequestedEvent>(e =>
                e.Email == "confirmed@test.com" &&
                e.ResetToken == "reset-token-123" &&
                e.ResetUrl.Contains("reset-password")));
    }

    #endregion

    #region ResetPassword - Event publishing

    [Fact]
    public async Task ResetPassword_UserNotFound_ReturnsBadRequestWithInvalidToken()
    {
        _userManager.FindByEmailAsync("missing@test.com").Returns((WallowUser?)null);

        IActionResult result = await _controller.ResetPassword(
            new AccountResetPasswordRequest("missing@test.com", "token", "NewPass1!"));

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("invalid_token");
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<PasswordChangedEvent>());
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequestAndDoesNotPublish()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _userManager.ResetPasswordAsync(user, "bad-token", "NewPass1!")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));

        IActionResult result = await _controller.ResetPassword(
            new AccountResetPasswordRequest("test@test.com", "bad-token", "NewPass1!"));

        result.Should().BeOfType<BadRequestObjectResult>();
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<PasswordChangedEvent>());
    }

    [Fact]
    public async Task ResetPassword_Success_PublishesPasswordChangedEvent()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _userManager.ResetPasswordAsync(user, "valid-token", "NewPass1!")
            .Returns(IdentityResult.Success);

        IActionResult result = await _controller.ResetPassword(
            new AccountResetPasswordRequest("test@test.com", "valid-token", "NewPass1!"));

        result.Should().BeOfType<OkObjectResult>();
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<PasswordChangedEvent>(e =>
                e.Email == "test@test.com" &&
                e.FirstName == "Test"));
    }

    #endregion

    #region VerifyEmail - Event publishing

    [Fact]
    public async Task VerifyEmail_UserNotFound_ReturnsBadRequestWithInvalidToken()
    {
        _userManager.FindByEmailAsync("nobody@test.com").Returns((WallowUser?)null);

        IActionResult result = await _controller.VerifyEmail("nobody@test.com", "token");

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("invalid_token");
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<EmailVerifiedEvent>());
    }

    [Fact]
    public async Task VerifyEmail_InvalidToken_ReturnsBadRequestAndDoesNotPublish()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _userManager.ConfirmEmailAsync(user, "bad-token")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));

        IActionResult result = await _controller.VerifyEmail("test@test.com", "bad-token");

        result.Should().BeOfType<BadRequestObjectResult>();
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<EmailVerifiedEvent>());
    }

    [Fact]
    public async Task VerifyEmail_Success_PublishesEmailVerifiedEvent()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _userManager.ConfirmEmailAsync(user, "valid-token")
            .Returns(IdentityResult.Success);

        IActionResult result = await _controller.VerifyEmail("test@test.com", "valid-token");

        result.Should().BeOfType<OkObjectResult>();
        await _messageBus.Received(1).PublishAsync(
            Arg.Is<EmailVerifiedEvent>(e =>
                e.Email == "test@test.com" &&
                e.FirstName == "Test" &&
                e.LastName == "User"));
    }

    #endregion

    #region GetClientTenant

    [Fact]
    public async Task GetClientTenant_NotFound_ReturnsNotFound()
    {
        _clientTenantResolver.ResolveAsync("unknown-client", Arg.Any<CancellationToken>())
            .Returns((ClientTenantInfo?)null);

        IActionResult result = await _controller.GetClientTenant("unknown-client");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetClientTenant_Found_ReturnsOkWithTenantInfo()
    {
        Guid tenantId = Guid.NewGuid();
        _clientTenantResolver.ResolveAsync("my-client", Arg.Any<CancellationToken>())
            .Returns(new ClientTenantInfo(tenantId, "My Org"));

        IActionResult result = await _controller.GetClientTenant("my-client");

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain(tenantId.ToString());
        json.Should().Contain("My Org");
    }

    #endregion

    #region SendMagicLink

    [Fact]
    public async Task SendMagicLink_Failure_ReturnsBadRequest()
    {
        _passwordlessService.SendMagicLinkAsync("test@test.com", Arg.Any<CancellationToken>())
            .Returns(new PasswordlessResult(false, null, "user_not_found"));

        IActionResult result = await _controller.SendMagicLink(
            new SendMagicLinkRequest("test@test.com"), CancellationToken.None);

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("user_not_found");
    }

    [Fact]
    public async Task SendMagicLink_Success_ReturnsOk()
    {
        _passwordlessService.SendMagicLinkAsync("test@test.com", Arg.Any<CancellationToken>())
            .Returns(new PasswordlessResult(true, "test@test.com", null));

        IActionResult result = await _controller.SendMagicLink(
            new SendMagicLinkRequest("test@test.com"), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("true");
    }

    #endregion

    #region VerifyMagicLink

    [Fact]
    public async Task VerifyMagicLink_Failure_ReturnsUnauthorized()
    {
        _passwordlessService.ValidateMagicLinkAsync("invalid-token", Arg.Any<CancellationToken>())
            .Returns(new PasswordlessResult(false, null, "invalid_or_expired_token"));

        IActionResult result = await _controller.VerifyMagicLink("invalid-token", CancellationToken.None);

        UnauthorizedObjectResult unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(unauthorized.Value);
        json.Should().Contain("invalid_or_expired_token");
    }

    [Fact]
    public async Task VerifyMagicLink_Success_ReturnsOkWithEmail()
    {
        _passwordlessService.ValidateMagicLinkAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(new PasswordlessResult(true, "test@test.com", null));

        IActionResult result = await _controller.VerifyMagicLink("valid-token", CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("test@test.com");
    }

    #endregion

    #region SendOtp

    [Fact]
    public async Task SendOtp_Failure_ReturnsBadRequest()
    {
        _passwordlessService.SendOtpAsync("test@test.com", Arg.Any<CancellationToken>())
            .Returns(new PasswordlessResult(false, null, "user_not_found"));

        IActionResult result = await _controller.SendOtp(
            new SendOtpRequest("test@test.com"), CancellationToken.None);

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("user_not_found");
    }

    [Fact]
    public async Task SendOtp_Success_ReturnsOk()
    {
        _passwordlessService.SendOtpAsync("test@test.com", Arg.Any<CancellationToken>())
            .Returns(new PasswordlessResult(true, "test@test.com", null));

        IActionResult result = await _controller.SendOtp(
            new SendOtpRequest("test@test.com"), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("true");
    }

    #endregion

    #region VerifyOtp

    [Fact]
    public async Task VerifyOtp_Failure_ReturnsUnauthorized()
    {
        _passwordlessService.ValidateOtpAsync("test@test.com", "000000", Arg.Any<CancellationToken>())
            .Returns(new PasswordlessResult(false, null, "invalid_code"));

        IActionResult result = await _controller.VerifyOtp(
            new VerifyOtpRequest("test@test.com", "000000"), CancellationToken.None);

        UnauthorizedObjectResult unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(unauthorized.Value);
        json.Should().Contain("invalid_code");
    }

    [Fact]
    public async Task VerifyOtp_Success_ReturnsOkWithEmail()
    {
        _passwordlessService.ValidateOtpAsync("test@test.com", "123456", Arg.Any<CancellationToken>())
            .Returns(new PasswordlessResult(true, "test@test.com", null));

        IActionResult result = await _controller.VerifyOtp(
            new VerifyOtpRequest("test@test.com", "123456"), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("test@test.com");
    }

    #endregion

    #region ExchangeTicket - Invalid ticket additional

    [Fact]
    public async Task ExchangeTicket_WithEmptyTicket_ReturnsBadRequest()
    {
        IActionResult result = await _controller.ExchangeTicket("", null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
