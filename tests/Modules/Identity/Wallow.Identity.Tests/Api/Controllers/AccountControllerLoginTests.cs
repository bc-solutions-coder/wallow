using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wolverine;
using AccountLoginRequest = Wallow.Identity.Api.Contracts.Requests.AccountLoginRequest;
using MfaVerifyRequest = Wallow.Identity.Api.Contracts.Requests.MfaVerifyRequest;

namespace Wallow.Identity.Tests.Api.Controllers;

public class AccountControllerLoginTests
{
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly UserManager<WallowUser> _userManager;
    private readonly IMfaPartialAuthService _mfaPartialAuthService;
    private readonly IMfaService _mfaService;
    private readonly IMfaExemptionChecker _mfaExemptionChecker;
    private readonly IOrganizationMfaPolicyService _orgMfaPolicyService;
    private readonly AccountController _controller;

    private const string TestEmail = "test@example.com";
    private const string TestPassword = "Password123!";

    public AccountControllerLoginTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _signInManager = Substitute.For<SignInManager<WallowUser>>(
            _userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<WallowUser>>(),
            null, null, null, null);

        IConfiguration configuration = Substitute.For<IConfiguration>();
        configuration["AuthUrl"].Returns("https://auth.example.com");

        IRedirectUriValidator redirectUriValidator = Substitute.For<IRedirectUriValidator>();
        IDataProtectionProvider dataProtectionProvider = Substitute.For<IDataProtectionProvider>();
        IAuthenticationSchemeProvider authSchemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        IMessageBus messageBus = Substitute.For<IMessageBus>();
        IClientTenantResolver clientTenantResolver = Substitute.For<IClientTenantResolver>();
        IOrganizationService organizationService = Substitute.For<IOrganizationService>();
        IPasswordlessService passwordlessService = Substitute.For<IPasswordlessService>();
        _mfaExemptionChecker = Substitute.For<IMfaExemptionChecker>();
        _mfaService = Substitute.For<IMfaService>();
        _mfaPartialAuthService = Substitute.For<IMfaPartialAuthService>();
        _orgMfaPolicyService = Substitute.For<IOrganizationMfaPolicyService>();
        ILogger<AccountController> logger = Substitute.For<ILogger<AccountController>>();
        TimeProvider timeProvider = TimeProvider.System;

        _controller = new AccountController(
            _signInManager,
            configuration,
            redirectUriValidator,
            dataProtectionProvider,
            authSchemeProvider,
            messageBus,
            clientTenantResolver,
            organizationService,
            passwordlessService,
            _mfaExemptionChecker,
            _mfaService,
            _mfaPartialAuthService,
            _orgMfaPolicyService,
            logger,
            timeProvider);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private WallowUser CreateTestUser(bool mfaEnabled = false, DateTimeOffset? mfaGraceDeadline = null)
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", TestEmail, TimeProvider.System);
        if (mfaEnabled)
        {
            // MfaEnabled is a private setter — use reflection or the domain method
            typeof(WallowUser).GetProperty(nameof(WallowUser.MfaEnabled))!
                .SetValue(user, true);
        }
        if (mfaGraceDeadline.HasValue)
        {
            typeof(WallowUser).GetProperty(nameof(WallowUser.MfaGraceDeadline))!
                .SetValue(user, mfaGraceDeadline.Value);
        }
        return user;
    }

    #region Login - MFA Required (partial cookie flow)

    [Fact]
    public async Task Login_WhenMfaEnabledAndNotExempt_ReturnsMfaRequiredAndIssuesPartialCookie()
    {
        WallowUser user = CreateTestUser(mfaEnabled: true);
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, TestPassword, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _mfaExemptionChecker.IsExemptAsync(user, Arg.Any<CancellationToken>()).Returns(false);
        _orgMfaPolicyService.CheckAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new OrgMfaPolicyResult(false, false));

        IActionResult result = await _controller.Login(
            new AccountLoginRequest(TestEmail, TestPassword, false), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"mfaRequired\":true");
        json.Should().NotContain("challengeToken");

        await _mfaPartialAuthService.Received(1).IssuePartialCookieAsync(
            Arg.Is<MfaPartialAuthPayload>(p =>
                p.UserId == user.Id.ToString() &&
                p.Email == TestEmail),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Login - MFA Disabled

    [Fact]
    public async Task Login_WhenMfaDisabled_ReturnsSucceeded()
    {
        WallowUser user = CreateTestUser(mfaEnabled: false);
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, TestPassword, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _orgMfaPolicyService.CheckAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new OrgMfaPolicyResult(false, false));

        IActionResult result = await _controller.Login(
            new AccountLoginRequest(TestEmail, TestPassword, false), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"succeeded\":true");
    }

    #endregion

    #region Login - Org Requires MFA, Grace Active

    [Fact]
    public async Task Login_WhenOrgRequiresMfaAndGraceActive_ReturnsMfaEnrollmentRequired()
    {
        WallowUser user = CreateTestUser(mfaEnabled: false);
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, TestPassword, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _orgMfaPolicyService.CheckAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new OrgMfaPolicyResult(RequiresMfa: true, IsInGracePeriod: true));

        IActionResult result = await _controller.Login(
            new AccountLoginRequest(TestEmail, TestPassword, false), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"mfaEnrollmentRequired\":true");
    }

    #endregion

    #region Login - Org Requires MFA, Grace Expired

    [Fact]
    public async Task Login_WhenOrgRequiresMfaAndGraceExpired_ReturnsMfaEnrollmentRequiredWithPartialCookie()
    {
        WallowUser user = CreateTestUser(mfaEnabled: false);
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, TestPassword, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _orgMfaPolicyService.CheckAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new OrgMfaPolicyResult(RequiresMfa: true, IsInGracePeriod: false));

        IActionResult result = await _controller.Login(
            new AccountLoginRequest(TestEmail, TestPassword, false), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"mfaEnrollmentRequired\":true");
        json.Should().Contain("\"succeeded\":false");

        await _mfaPartialAuthService.Received(1).IssuePartialCookieAsync(
            Arg.Is<MfaPartialAuthPayload>(p => p.UserId == user.Id.ToString()),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region VerifyMfaChallenge - No Partial Cookie

    [Fact]
    public async Task VerifyMfaChallenge_WhenPartialCookieAbsent_Returns401()
    {
        _mfaPartialAuthService.ValidatePartialCookieAsync(Arg.Any<CancellationToken>())
            .Returns((MfaPartialAuthPayload?)null);

        IActionResult result = await _controller.VerifyMfaChallenge(
            new MfaVerifyRequest("123456"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region VerifyMfaChallenge - Valid TOTP

    [Fact]
    public async Task VerifyMfaChallenge_WithValidTotp_UpgradesToFullAuthAndReturnsSuccess()
    {
        string userId = Guid.NewGuid().ToString();
        MfaPartialAuthPayload payload = new(userId, TestEmail, "password", false, DateTimeOffset.UtcNow);
        _mfaPartialAuthService.ValidatePartialCookieAsync(Arg.Any<CancellationToken>())
            .Returns(payload);

        WallowUser user = WallowUser.Create(Guid.Parse(userId), "Test", "User", TestEmail, TimeProvider.System);
        typeof(WallowUser).GetProperty("TotpSecretEncrypted")!.SetValue(user, "encrypted-secret");
        _signInManager.UserManager.FindByIdAsync(userId).Returns(user);

        _mfaService.ValidateTotpAsync("encrypted-secret", "123456", Arg.Any<CancellationToken>())
            .Returns(true);

        IActionResult result = await _controller.VerifyMfaChallenge(
            new MfaVerifyRequest("123456"), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"succeeded\":true");

        await _mfaPartialAuthService.Received(1).UpgradeToFullAuthAsync(
            userId, false, Arg.Any<CancellationToken>());
    }

    #endregion
}
