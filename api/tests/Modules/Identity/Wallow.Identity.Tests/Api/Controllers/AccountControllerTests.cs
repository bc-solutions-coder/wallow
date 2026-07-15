using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

public class AccountControllerTests
{
    private readonly AccountController _controller;
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly UserManager<WallowUser> _userManager;
    private readonly IRedirectUriValidator _redirectUriValidator;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public AccountControllerTests()
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
        _dataProtectionProvider = Substitute.For<IDataProtectionProvider>();

        _controller = new AccountController(
            _signInManager,
            configuration,
            _redirectUriValidator,
            _dataProtectionProvider,
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IMessageBus>(),
            Substitute.For<IClientTenantResolver>(),
            Substitute.For<IOrganizationService>(),
            Substitute.For<IPasswordlessService>(),
            Substitute.For<IMfaExemptionChecker>(),
            Substitute.For<IMfaService>(),
            Substitute.For<IMfaPartialAuthService>(),
            Substitute.For<IOrganizationMfaPolicyService>(),
            Substitute.For<IMfaLockoutService>(),
            Substitute.For<IConnectionMultiplexer>(),
            Substitute.For<ILogger<AccountController>>(),
            TimeProvider.System);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region Login

    [Fact]
    public async Task Login_WithNullUser_ReturnsUnauthorized()
    {
        _userManager.FindByEmailAsync("unknown@test.com").Returns((WallowUser?)null);

        IActionResult result = await _controller.Login(new AccountLoginRequest("unknown@test.com", "password", false), CancellationToken.None);

        UnauthorizedObjectResult unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(unauthorized.Value);
        json.Should().Contain("invalid_credentials");
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithTicket()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, "password", true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        IDataProtector innerProtector = Substitute.For<IDataProtector>();
        _dataProtectionProvider.CreateProtector("SignInTicket").Returns(innerProtector);
        innerProtector.CreateProtector("").Returns(innerProtector);

        // The ToTimeLimitedDataProtector extension creates a wrapper, so we mock the Protect call on the base protector
        innerProtector.Protect(Arg.Any<byte[]>()).Returns(new byte[] { 1, 2, 3 });

        IActionResult result = await _controller.Login(new AccountLoginRequest("test@test.com", "password", false), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("succeeded");
    }

    [Fact]
    public async Task Login_WithLockedOutUser_Returns423()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "locked@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("locked@test.com").Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, "password", true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        IActionResult result = await _controller.Login(new AccountLoginRequest("locked@test.com", "password", false), CancellationToken.None);

        ObjectResult statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(423);
    }

    [Fact]
    public async Task Login_WithNotAllowedUser_Returns403()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "unconfirmed@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("unconfirmed@test.com").Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, "password", true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.NotAllowed);

        IActionResult result = await _controller.Login(new AccountLoginRequest("unconfirmed@test.com", "password", false), CancellationToken.None);

        ObjectResult statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, "wrong", true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        IActionResult result = await _controller.Login(new AccountLoginRequest("test@test.com", "wrong", false), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region Register

    [Fact]
    public async Task Register_WithMismatchedPasswords_ReturnsBadRequest()
    {
        IActionResult result = await _controller.Register(
            new AccountRegisterRequest("test@test.com", "Password1!", "DifferentPassword1!"));

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("passwords_do_not_match");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequestWithEmailTaken()
    {
        _userManager.CreateAsync(Arg.Any<WallowUser>(), "Password1!")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = "Email taken" }));

        IActionResult result = await _controller.Register(
            new AccountRegisterRequest("test@test.com", "Password1!", "Password1!"));

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("email_taken");
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsOk()
    {
        _userManager.CreateAsync(Arg.Any<WallowUser>(), "Password1!")
            .Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<WallowUser>(), "user")
            .Returns(IdentityResult.Success);
        _userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<WallowUser>())
            .Returns("token123");

        IActionResult result = await _controller.Register(
            new AccountRegisterRequest("test@test.com", "Password1!", "Password1!"));

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("succeeded");
    }

    #endregion

    #region ForgotPassword

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_ReturnsOk()
    {
        _userManager.FindByEmailAsync("unknown@test.com").Returns((WallowUser?)null);

        IActionResult result = await _controller.ForgotPassword(
            new AccountForgotPasswordRequest("unknown@test.com"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ForgotPassword_WithValidEmail_ReturnsOk()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _userManager.IsEmailConfirmedAsync(user).Returns(true);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token");

        IActionResult result = await _controller.ForgotPassword(
            new AccountForgotPasswordRequest("test@test.com"));

        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region ResetPassword

    [Fact]
    public async Task ResetPassword_WithUnknownUser_ReturnsBadRequest()
    {
        _userManager.FindByEmailAsync("unknown@test.com").Returns((WallowUser?)null);

        IActionResult result = await _controller.ResetPassword(
            new AccountResetPasswordRequest("unknown@test.com", "token", "NewPassword1!"));

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("invalid_token");
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequest()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _userManager.ResetPasswordAsync(user, "bad-token", "NewPassword1!")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));

        IActionResult result = await _controller.ResetPassword(
            new AccountResetPasswordRequest("test@test.com", "bad-token", "NewPassword1!"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ReturnsOk()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _userManager.ResetPasswordAsync(user, "valid-token", "NewPassword1!")
            .Returns(IdentityResult.Success);

        IActionResult result = await _controller.ResetPassword(
            new AccountResetPasswordRequest("test@test.com", "valid-token", "NewPassword1!"));

        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region VerifyEmail

    [Fact]
    public async Task VerifyEmail_WithUnknownUser_ReturnsBadRequest()
    {
        _userManager.FindByEmailAsync("unknown@test.com").Returns((WallowUser?)null);

        IActionResult result = await _controller.VerifyEmail("unknown@test.com", "token");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_ReturnsBadRequest()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _userManager.ConfirmEmailAsync(user, "bad-token")
            .Returns(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));

        IActionResult result = await _controller.VerifyEmail("test@test.com", "bad-token");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task VerifyEmail_WithValidToken_ReturnsOk()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByEmailAsync("test@test.com").Returns(user);
        _userManager.ConfirmEmailAsync(user, "valid-token")
            .Returns(IdentityResult.Success);

        IActionResult result = await _controller.VerifyEmail("test@test.com", "valid-token");

        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region ValidateRedirectUri

    [Fact]
    public async Task ValidateRedirectUri_WithNullUri_ReturnsFalse()
    {
        IActionResult result = await _controller.ValidateRedirectUri(null, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("false");
    }

    [Fact]
    public async Task ValidateRedirectUri_WithAllowedUri_ReturnsTrue()
    {
        _redirectUriValidator.IsAllowedAsync("http://localhost:5002", Arg.Any<CancellationToken>())
            .Returns(true);

        IActionResult result = await _controller.ValidateRedirectUri("http://localhost:5002", CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("true");
    }

    [Fact]
    public async Task ValidateRedirectUri_WithDisallowedUri_ReturnsFalse()
    {
        _redirectUriValidator.IsAllowedAsync("http://evil.com", Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await _controller.ValidateRedirectUri("http://evil.com", CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("false");
    }

    #endregion

    #region SignOut

    [Fact]
    public async Task SignOut_WithNoRedirectUri_RedirectsToLogout()
    {
        IActionResult result = await _controller.SignOut(null);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("logout?signed_out=true");
    }

    [Fact]
    public async Task SignOut_WithInvalidRedirectUri_RedirectsToError()
    {
        _redirectUriValidator.IsAllowedAsync("http://evil.com", Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await _controller.SignOut("http://evil.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("error?reason=invalid_redirect_uri");
    }

    [Fact]
    public async Task SignOut_WithValidRedirectUri_RedirectsWithPostLogoutUri()
    {
        _redirectUriValidator.IsAllowedAsync("http://app.test.com", Arg.Any<CancellationToken>())
            .Returns(true);

        IActionResult result = await _controller.SignOut("http://app.test.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("signed_out=true");
        redirect.Url.Should().Contain("post_logout_redirect_uri=");
    }

    #endregion

    #region ExchangeTicket

    [Fact]
    public async Task ExchangeTicket_WithInvalidTicket_ReturnsBadRequest()
    {
        // The data protection provider will throw when trying to unprotect invalid data
        IDataProtector innerProtector = Substitute.For<IDataProtector>();
        _dataProtectionProvider.CreateProtector("SignInTicket").Returns(innerProtector);
        innerProtector.CreateProtector("").Returns(innerProtector);
        innerProtector.When(p => p.Unprotect(Arg.Any<byte[]>()))
            .Do(_ => throw new System.Security.Cryptography.CryptographicException());

        IActionResult result = await _controller.ExchangeTicket("invalid-ticket", null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
