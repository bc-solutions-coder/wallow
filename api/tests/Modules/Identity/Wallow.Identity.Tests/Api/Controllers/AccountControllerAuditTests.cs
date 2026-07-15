using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;
using AccountLoginRequest = Wallow.Identity.Api.Contracts.Requests.AccountLoginRequest;
using MfaVerifyRequest = Wallow.Identity.Api.Contracts.Requests.MfaVerifyRequest;

namespace Wallow.Identity.Tests.Api.Controllers;

public class AccountControllerAuditTests
{
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly UserManager<WallowUser> _userManager;
    private readonly IMessageBus _messageBus;
    private readonly IMfaExemptionChecker _mfaExemptionChecker;
    private readonly IMfaPartialAuthService _mfaPartialAuthService;
    private readonly IMfaService _mfaService;
    private readonly IOrganizationMfaPolicyService _orgMfaPolicyService;
    private readonly IMfaLockoutService _mfaLockoutService;
    private readonly AccountController _controller;

    private const string TestEmail = "test@example.com";
    private const string TestPassword = "Password123!";
    private const string TestIpAddress = "192.168.1.100";

    public AccountControllerAuditTests()
    {
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);

        _signInManager = Substitute.For<SignInManager<WallowUser>>(
            _userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<WallowUser>>(),
            null, null, null, null);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthUrl"] = "https://auth.example.com"
            })
            .Build();

        _messageBus = Substitute.For<IMessageBus>();
        _mfaExemptionChecker = Substitute.For<IMfaExemptionChecker>();
        _mfaService = Substitute.For<IMfaService>();
        _mfaPartialAuthService = Substitute.For<IMfaPartialAuthService>();
        _orgMfaPolicyService = Substitute.For<IOrganizationMfaPolicyService>();
        _mfaLockoutService = Substitute.For<IMfaLockoutService>();

        _controller = new AccountController(
            _signInManager,
            configuration,
            Substitute.For<IRedirectUriValidator>(),
            new EphemeralDataProtectionProvider(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            _messageBus,
            Substitute.For<IClientTenantResolver>(),
            Substitute.For<IOrganizationService>(),
            Substitute.For<IPasswordlessService>(),
            _mfaExemptionChecker,
            _mfaService,
            _mfaPartialAuthService,
            _orgMfaPolicyService,
            _mfaLockoutService,
            Substitute.For<IConnectionMultiplexer>(),
            Substitute.For<ILogger<AccountController>>(),
            TimeProvider.System);

        DefaultHttpContext httpContext = new();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(TestIpAddress);
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private WallowUser CreateTestUser()
    {
        return WallowUser.Create(Guid.NewGuid(), "Test", "User", TestEmail, TimeProvider.System);
    }

    #region Login - Success audit event

    [Fact]
    public async Task Login_OnSuccess_PublishesUserLoginSucceededEvent()
    {
        WallowUser user = CreateTestUser();
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, TestPassword, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _orgMfaPolicyService.CheckAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new OrgMfaPolicyResult(false, false));

        await _controller.Login(new AccountLoginRequest(TestEmail, TestPassword, false), CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(Arg.Is<UserLoginSucceededEvent>(e =>
            e.UserId == user.Id &&
            e.TenantId == user.TenantId &&
            e.IpAddress == TestIpAddress));
    }

    [Fact]
    public async Task Login_OnSuccessWithMfaRequired_DoesNotPublishSucceededEvent()
    {
        WallowUser user = CreateTestUser();
        typeof(WallowUser).GetProperty(nameof(WallowUser.MfaEnabled))!.SetValue(user, true);
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, TestPassword, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _mfaExemptionChecker.IsExemptAsync(user, Arg.Any<CancellationToken>()).Returns(false);

        await _controller.Login(new AccountLoginRequest(TestEmail, TestPassword, false), CancellationToken.None);

        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<UserLoginSucceededEvent>());
    }

    #endregion

    #region Login - Failed audit event

    [Fact]
    public async Task Login_OnInvalidCredentials_PublishesUserLoginFailedEvent()
    {
        WallowUser user = CreateTestUser();
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, TestPassword, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        await _controller.Login(new AccountLoginRequest(TestEmail, TestPassword, false), CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(Arg.Is<UserLoginFailedEvent>(e =>
            e.UserId == user.Id &&
            e.TenantId == user.TenantId &&
            e.IpAddress == TestIpAddress &&
            e.Reason == "invalid_credentials"));
    }

    [Fact]
    public async Task Login_OnAccountNotFound_PublishesUserLoginFailedEvent()
    {
        _userManager.FindByEmailAsync(TestEmail).Returns((WallowUser?)null);

        await _controller.Login(new AccountLoginRequest(TestEmail, TestPassword, false), CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(Arg.Is<UserLoginFailedEvent>(e =>
            e.Reason == "account_not_found" &&
            e.IpAddress == TestIpAddress));
    }

    #endregion

    #region Login - Lockout audit event

    [Fact]
    public async Task Login_OnLockedOut_PublishesUserAccountLockedOutEvent()
    {
        WallowUser user = CreateTestUser();
        _userManager.FindByEmailAsync(TestEmail).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, TestPassword, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        await _controller.Login(new AccountLoginRequest(TestEmail, TestPassword, false), CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(Arg.Is<UserAccountLockedOutEvent>(e =>
            e.UserId == user.Id &&
            e.TenantId == user.TenantId &&
            e.IpAddress == TestIpAddress));
    }

    #endregion

    #region VerifyMfaChallenge - Success audit event

    [Fact]
    public async Task VerifyMfaChallenge_OnSuccess_PublishesUserLoginSucceededEvent()
    {
        string userId = Guid.NewGuid().ToString();
        MfaPartialAuthPayload payload = new(userId, TestEmail, "password", false, DateTimeOffset.UtcNow);
        _mfaPartialAuthService.ValidatePartialCookieAsync(Arg.Any<CancellationToken>()).Returns(payload);

        WallowUser user = WallowUser.Create(Guid.Parse(userId), "Test", "User", TestEmail, TimeProvider.System);
        typeof(WallowUser).GetProperty("TotpSecretEncrypted")!.SetValue(user, "encrypted-secret");
        _signInManager.UserManager.FindByIdAsync(userId).Returns(user);

        _mfaService.ValidateTotpAsync("encrypted-secret", "123456", Arg.Any<CancellationToken>()).Returns(true);

        await _controller.VerifyMfaChallenge(new MfaVerifyRequest("123456"), CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(Arg.Is<UserLoginSucceededEvent>(e =>
            e.UserId == user.Id &&
            e.TenantId == user.TenantId &&
            e.IpAddress == TestIpAddress));
    }

    #endregion

    #region VerifyMfaChallenge - Failed audit event

    [Fact]
    public async Task VerifyMfaChallenge_OnInvalidCode_PublishesUserLoginFailedEvent()
    {
        string userId = Guid.NewGuid().ToString();
        MfaPartialAuthPayload payload = new(userId, TestEmail, "password", false, DateTimeOffset.UtcNow);
        _mfaPartialAuthService.ValidatePartialCookieAsync(Arg.Any<CancellationToken>()).Returns(payload);

        WallowUser user = WallowUser.Create(Guid.Parse(userId), "Test", "User", TestEmail, TimeProvider.System);
        typeof(WallowUser).GetProperty("TotpSecretEncrypted")!.SetValue(user, "encrypted-secret");
        _signInManager.UserManager.FindByIdAsync(userId).Returns(user);

        _mfaService.ValidateTotpAsync("encrypted-secret", "wrong", Arg.Any<CancellationToken>()).Returns(false);
        _mfaService.ValidateBackupCodeAsync(userId, "wrong", Arg.Any<CancellationToken>()).Returns(false);
        _mfaLockoutService.RecordFailureAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new MfaLockoutResult(false, 0, 0, null));

        await _controller.VerifyMfaChallenge(new MfaVerifyRequest("wrong"), CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(Arg.Is<UserLoginFailedEvent>(e =>
            e.UserId == user.Id &&
            e.Reason == "invalid_mfa_code" &&
            e.IpAddress == TestIpAddress));
    }

    #endregion
}
