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
using MfaVerifyRequest = Wallow.Identity.Api.Contracts.Requests.MfaVerifyRequest;

namespace Wallow.Identity.Tests.Api.Controllers;

public class AccountControllerMfaLockoutTests
{
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly IMfaPartialAuthService _mfaPartialAuthService;
    private readonly IMfaService _mfaService;
    private readonly IMfaLockoutService _mfaLockoutService;
    private readonly IMessageBus _messageBus;
    private readonly TimeProvider _timeProvider;
    private readonly AccountController _controller;

    private const string TestEmail = "lockout@example.com";

    public AccountControllerMfaLockoutTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        UserManager<WallowUser> userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _signInManager = Substitute.For<SignInManager<WallowUser>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<WallowUser>>(),
            null, null, null, null);

        IConfiguration configuration = Substitute.For<IConfiguration>();
        configuration["AuthUrl"].Returns("https://auth.example.com");

        _messageBus = Substitute.For<IMessageBus>();
        _mfaService = Substitute.For<IMfaService>();
        _mfaPartialAuthService = Substitute.For<IMfaPartialAuthService>();
        _mfaLockoutService = Substitute.For<IMfaLockoutService>();
        _timeProvider = Substitute.For<TimeProvider>();
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);

        _controller = new AccountController(
            _signInManager,
            configuration,
            Substitute.For<IRedirectUriValidator>(),
            Substitute.For<IDataProtectionProvider>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            _messageBus,
            Substitute.For<IClientTenantResolver>(),
            Substitute.For<IOrganizationService>(),
            Substitute.For<IPasswordlessService>(),
            Substitute.For<IMfaExemptionChecker>(),
            _mfaService,
            _mfaPartialAuthService,
            Substitute.For<IOrganizationMfaPolicyService>(),
            _mfaLockoutService,
            Substitute.For<IConnectionMultiplexer>(),
            Substitute.For<ILogger<AccountController>>(),
            _timeProvider);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private WallowUser CreateTestUser(string? userId = null, bool lockedOut = false)
    {
        Guid id = userId is not null ? Guid.Parse(userId) : Guid.NewGuid();
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", TestEmail, TimeProvider.System);
        // Set the Id via reflection since it's set during Create
        typeof(WallowUser).GetProperty("Id")!.SetValue(user, id);
        typeof(WallowUser).GetProperty("TotpSecretEncrypted")!.SetValue(user, "encrypted-secret");

        if (lockedOut)
        {
            typeof(WallowUser).GetProperty("MfaLockoutEnd")!
                .SetValue(user, DateTimeOffset.UtcNow.AddMinutes(15));
        }

        return user;
    }

    private MfaPartialAuthPayload CreatePayload(string userId) =>
        new(userId, TestEmail, "password", false, DateTimeOffset.UtcNow);

    #region Already locked out returns 423

    [Fact]
    public async Task VerifyMfaChallenge_WhenAlreadyLockedOut_Returns423()
    {
        string userId = Guid.NewGuid().ToString();
        MfaPartialAuthPayload payload = CreatePayload(userId);
        _mfaPartialAuthService.ValidatePartialCookieAsync(Arg.Any<CancellationToken>())
            .Returns(payload);

        WallowUser user = CreateTestUser(userId, lockedOut: true);
        _signInManager.UserManager.FindByIdAsync(userId).Returns(user);

        IActionResult result = await _controller.VerifyMfaChallenge(
            new MfaVerifyRequest("123456"), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(423);
        string json = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        json.Should().Contain("\"error\":\"mfa_locked_out\"");
    }

    #endregion

    #region Invalid code crosses threshold returns 423

    [Fact]
    public async Task VerifyMfaChallenge_WhenInvalidCodeCrossesThreshold_Returns423()
    {
        string userId = Guid.NewGuid().ToString();
        MfaPartialAuthPayload payload = CreatePayload(userId);
        _mfaPartialAuthService.ValidatePartialCookieAsync(Arg.Any<CancellationToken>())
            .Returns(payload);

        WallowUser user = CreateTestUser(userId);
        _signInManager.UserManager.FindByIdAsync(userId).Returns(user);

        _mfaService.ValidateTotpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _mfaService.ValidateBackupCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _mfaLockoutService.RecordFailureAsync(user.Id, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new MfaLockoutResult(IsLockedOut: true, FailedAttempts: 5, LockoutCount: 1, LockoutEnd: DateTimeOffset.UtcNow.AddMinutes(15)));

        IActionResult result = await _controller.VerifyMfaChallenge(
            new MfaVerifyRequest("000000"), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(423);
        string json = System.Text.Json.JsonSerializer.Serialize(objectResult.Value);
        json.Should().Contain("\"error\":\"mfa_locked_out\"");
    }

    #endregion

    #region Valid code resets lockout counter

    [Fact]
    public async Task VerifyMfaChallenge_WhenValidCode_CallsResetAsync()
    {
        string userId = Guid.NewGuid().ToString();
        MfaPartialAuthPayload payload = CreatePayload(userId);
        _mfaPartialAuthService.ValidatePartialCookieAsync(Arg.Any<CancellationToken>())
            .Returns(payload);

        WallowUser user = CreateTestUser(userId);
        _signInManager.UserManager.FindByIdAsync(userId).Returns(user);

        _mfaService.ValidateTotpAsync("encrypted-secret", "123456", Arg.Any<CancellationToken>())
            .Returns(true);

        IActionResult result = await _controller.VerifyMfaChallenge(
            new MfaVerifyRequest("123456"), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"succeeded\":true");

        await _mfaLockoutService.Received(1).ResetAsync(user.Id, Arg.Any<CancellationToken>());
    }

    #endregion

    #region Event published on new lockout

    [Fact]
    public async Task VerifyMfaChallenge_WhenNewLockoutOccurs_PublishesUserMfaLockedOutEvent()
    {
        string userId = Guid.NewGuid().ToString();
        MfaPartialAuthPayload payload = CreatePayload(userId);
        _mfaPartialAuthService.ValidatePartialCookieAsync(Arg.Any<CancellationToken>())
            .Returns(payload);

        WallowUser user = CreateTestUser(userId);
        _signInManager.UserManager.FindByIdAsync(userId).Returns(user);

        _mfaService.ValidateTotpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _mfaService.ValidateBackupCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _mfaLockoutService.RecordFailureAsync(user.Id, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new MfaLockoutResult(IsLockedOut: true, FailedAttempts: 5, LockoutCount: 2, LockoutEnd: DateTimeOffset.UtcNow.AddMinutes(15)));

        await _controller.VerifyMfaChallenge(
            new MfaVerifyRequest("000000"), CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<UserMfaLockedOutEvent>(e =>
                e.UserId == user.Id &&
                e.LockoutCount == 2));
    }

    #endregion

    #region No event on plain invalid code below threshold

    [Fact]
    public async Task VerifyMfaChallenge_WhenInvalidCodeBelowThreshold_DoesNotPublishEvent()
    {
        string userId = Guid.NewGuid().ToString();
        MfaPartialAuthPayload payload = CreatePayload(userId);
        _mfaPartialAuthService.ValidatePartialCookieAsync(Arg.Any<CancellationToken>())
            .Returns(payload);

        WallowUser user = CreateTestUser(userId);
        _signInManager.UserManager.FindByIdAsync(userId).Returns(user);

        _mfaService.ValidateTotpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _mfaService.ValidateBackupCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _mfaLockoutService.RecordFailureAsync(user.Id, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new MfaLockoutResult(IsLockedOut: false, FailedAttempts: 2, LockoutCount: 0, LockoutEnd: null));

        await _controller.VerifyMfaChallenge(
            new MfaVerifyRequest("000000"), CancellationToken.None);

        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<UserMfaLockedOutEvent>());
    }

    #endregion
}
