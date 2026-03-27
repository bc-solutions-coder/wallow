using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

public class MfaControllerTests
{
    private readonly IMfaService _mfaService;
    private readonly IMfaPartialAuthService _mfaPartialAuthService = Substitute.For<IMfaPartialAuthService>();
    private readonly UserManager<WallowUser> _userManager;
    private readonly IMessageBus _messageBus;
    private readonly ITenantContext _tenantContext;
    private readonly MfaController _controller;
    private const string TestUserId = "550e8400-e29b-41d4-a716-446655440000";
    private static readonly Guid _testTenantId = Guid.Parse("660e8400-e29b-41d4-a716-446655440000");

    public MfaControllerTests()
    {
        _mfaService = Substitute.For<IMfaService>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);
        _messageBus = Substitute.For<IMessageBus>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(new Wallow.Shared.Kernel.Identity.TenantId(_testTenantId));

        _mfaService.SerializeBackupCodesForStorage(Arg.Any<IReadOnlyList<string>>())
            .Returns("[]");

        IDataProtectionProvider dataProtectionProvider = Substitute.For<IDataProtectionProvider>();
        ILogger<MfaController> logger = Substitute.For<ILogger<MfaController>>();
        _controller = new MfaController(_mfaService, _mfaPartialAuthService, _userManager, _messageBus, _tenantContext, dataProtectionProvider, logger);

        ClaimsIdentity identity = new(
            [new Claim(ClaimTypes.NameIdentifier, TestUserId)],
            "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    #region EnrollTotp

    [Fact]
    public async Task EnrollTotp_ReturnsSecretAndQrUri()
    {
        _mfaService.GenerateEnrollmentSecretAsync(TestUserId, Arg.Any<CancellationToken>())
            .Returns(("JBSWY3DPEHPK3PXP", "otpauth://totp/Wallow:test@test.com?secret=JBSWY3DPEHPK3PXP"));

        IActionResult result = await _controller.EnrollTotp(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("JBSWY3DPEHPK3PXP");
        json.Should().Contain("otpauth://");
    }

    #endregion

    #region ConfirmEnrollment

    [Fact]
    public async Task ConfirmEnrollment_WithInvalidCode_ReturnsBadRequest()
    {
        _mfaService.ValidateTotpAsync("secret", "000000", Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await _controller.ConfirmEnrollment(
            new MfaConfirmRequest("secret", "000000"), CancellationToken.None);

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("invalid_code");
    }

    [Fact]
    public async Task ConfirmEnrollment_WithUserNotFound_ReturnsBadRequest()
    {
        _mfaService.ValidateTotpAsync("secret", "123456", Arg.Any<CancellationToken>())
            .Returns(true);
        _userManager.FindByIdAsync(TestUserId).Returns((WallowUser?)null);

        IActionResult result = await _controller.ConfirmEnrollment(
            new MfaConfirmRequest("secret", "123456"), CancellationToken.None);

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("user_not_found");
    }

    [Fact]
    public async Task ConfirmEnrollment_WithUpdateFailure_ReturnsBadRequest()
    {
        _mfaService.ValidateTotpAsync("secret", "123456", Arg.Any<CancellationToken>())
            .Returns(true);
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByIdAsync(TestUserId).Returns(user);
        _mfaService.GenerateBackupCodesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string> { "code1", "code2" });
        _userManager.UpdateAsync(user)
            .Returns(IdentityResult.Failed(new IdentityError { Code = "Error" }));

        IActionResult result = await _controller.ConfirmEnrollment(
            new MfaConfirmRequest("secret", "123456"), CancellationToken.None);

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("update_failed");
    }

    [Fact]
    public async Task ConfirmEnrollment_WithValidCode_ReturnsOkWithBackupCodes()
    {
        _mfaService.ValidateTotpAsync("secret", "123456", Arg.Any<CancellationToken>())
            .Returns(true);
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByIdAsync(TestUserId).Returns(user);
        List<string> backupCodes = ["code1", "code2", "code3"];
        _mfaService.GenerateBackupCodesAsync(Arg.Any<CancellationToken>())
            .Returns(backupCodes);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        IActionResult result = await _controller.ConfirmEnrollment(
            new MfaConfirmRequest("secret", "123456"), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("succeeded");
        json.Should().Contain("code1");
    }

    #endregion

    #region NoUserClaim

    [Fact]
    public async Task EnrollTotp_WithoutUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        IActionResult result = await _controller.EnrollTotp(CancellationToken.None);

        UnauthorizedObjectResult unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(unauthorized.Value);
        json.Should().Contain("no_auth_session");
    }

    #endregion

    #region Disable

    [Fact]
    public async Task Disable_WithWrongPassword_ReturnsBadRequest()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        user.EnableMfa("totp", "encrypted-secret");
        _userManager.FindByIdAsync(TestUserId).Returns(user);
        _userManager.CheckPasswordAsync(user, "wrong-password").Returns(false);

        IActionResult result = await _controller.Disable(
            new MfaDisableRequest("wrong-password"), CancellationToken.None);

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("invalid_password");
    }

    [Fact]
    public async Task Disable_WhenMfaNotEnabled_ReturnsBadRequest()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByIdAsync(TestUserId).Returns(user);
        _userManager.CheckPasswordAsync(user, "correct-password").Returns(true);

        IActionResult result = await _controller.Disable(
            new MfaDisableRequest("correct-password"), CancellationToken.None);

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("mfa_not_enabled");
    }

    [Fact]
    public async Task Disable_WithValidPassword_ReturnsOkWithSucceeded()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        user.EnableMfa("totp", "encrypted-secret");
        _userManager.FindByIdAsync(TestUserId).Returns(user);
        _userManager.CheckPasswordAsync(user, "correct-password").Returns(true);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        IActionResult result = await _controller.Disable(
            new MfaDisableRequest("correct-password"), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("succeeded");
    }

    #endregion

    #region RegenerateBackupCodes

    [Fact]
    public async Task RegenerateBackupCodes_WithWrongPassword_ReturnsBadRequest()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByIdAsync(TestUserId).Returns(user);
        _userManager.CheckPasswordAsync(user, "wrong-password").Returns(false);

        IActionResult result = await _controller.RegenerateBackupCodes(
            new MfaRegenerateBackupCodesRequest("wrong-password"), CancellationToken.None);

        BadRequestObjectResult bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(bad.Value);
        json.Should().Contain("invalid_password");
    }

    [Fact]
    public async Task RegenerateBackupCodes_WithValidPassword_ReturnsNewCodes()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        user.EnableMfa("totp", "encrypted-secret");
        _userManager.FindByIdAsync(TestUserId).Returns(user);
        _userManager.CheckPasswordAsync(user, "correct-password").Returns(true);
        List<string> newCodes = ["newcode1", "newcode2", "newcode3"];
        _mfaService.GenerateBackupCodesAsync(Arg.Any<CancellationToken>()).Returns(newCodes);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        IActionResult result = await _controller.RegenerateBackupCodes(
            new MfaRegenerateBackupCodesRequest("correct-password"), CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("newcode1");
    }

    #endregion

    #region AdminDisableMfa

    [Fact]
    public async Task AdminDisableMfa_WhenUserNotFound_ReturnsNotFound()
    {
        string targetUserId = Guid.NewGuid().ToString();
        _userManager.FindByIdAsync(targetUserId).Returns((WallowUser?)null);

        IActionResult result = await _controller.AdminDisableMfa(targetUserId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AdminDisableMfa_WhenUserFound_ReturnsOk()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        user.EnableMfa("totp", "encrypted-secret");
        string targetUserId = user.Id.ToString();
        _userManager.FindByIdAsync(targetUserId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        IActionResult result = await _controller.AdminDisableMfa(targetUserId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("succeeded");
    }

    #endregion

    #region AdminClearLockout

    [Fact]
    public async Task AdminClearLockout_WhenUserFound_ReturnsOk()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        string targetUserId = user.Id.ToString();
        _userManager.FindByIdAsync(targetUserId).Returns(user);
        _userManager.SetLockoutEndDateAsync(user, null).Returns(IdentityResult.Success);
        _userManager.ResetAccessFailedCountAsync(user).Returns(IdentityResult.Success);

        IActionResult result = await _controller.AdminClearLockout(targetUserId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        string json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("succeeded");
    }

    #endregion

    #region AuditEvents

    [Fact]
    public async Task ConfirmEnrollment_OnSuccess_PublishesUserMfaEnabledEvent()
    {
        _mfaService.ValidateTotpAsync("secret", "123456", Arg.Any<CancellationToken>())
            .Returns(true);
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        _userManager.FindByIdAsync(TestUserId).Returns(user);
        _mfaService.GenerateBackupCodesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string> { "code1", "code2" });
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _controller.ConfirmEnrollment(
            new MfaConfirmRequest("secret", "123456"), CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<UserMfaEnabledEvent>(e =>
                e.UserId == Guid.Parse(TestUserId) &&
                e.TenantId == _testTenantId));
    }

    [Fact]
    public async Task Disable_OnSuccess_PublishesUserMfaDisabledEvent()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        user.EnableMfa("totp", "encrypted-secret");
        _userManager.FindByIdAsync(TestUserId).Returns(user);
        _userManager.CheckPasswordAsync(user, "correct-password").Returns(true);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _controller.Disable(
            new MfaDisableRequest("correct-password"), CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<UserMfaDisabledEvent>(e =>
                e.UserId == Guid.Parse(TestUserId) &&
                e.TenantId == _testTenantId));
    }

    [Fact]
    public async Task Disable_WithWrongPassword_DoesNotPublishEvent()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        user.EnableMfa("totp", "encrypted-secret");
        _userManager.FindByIdAsync(TestUserId).Returns(user);
        _userManager.CheckPasswordAsync(user, "wrong-password").Returns(false);

        await _controller.Disable(
            new MfaDisableRequest("wrong-password"), CancellationToken.None);

        await _messageBus.DidNotReceive().PublishAsync(
            Arg.Any<UserMfaDisabledEvent>());
    }

    [Fact]
    public async Task AdminClearLockout_OnSuccess_PublishesUserMfaLockoutClearedEvent()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        string targetUserId = user.Id.ToString();
        _userManager.FindByIdAsync(targetUserId).Returns(user);
        _userManager.SetLockoutEndDateAsync(user, null).Returns(IdentityResult.Success);
        _userManager.ResetAccessFailedCountAsync(user).Returns(IdentityResult.Success);

        await _controller.AdminClearLockout(targetUserId, CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<UserMfaLockoutClearedEvent>(e =>
                e.UserId == user.Id &&
                e.TenantId == _testTenantId &&
                e.ClearedByUserId == Guid.Parse(TestUserId)));
    }

    [Fact]
    public async Task RegenerateBackupCodes_OnSuccess_PublishesUserMfaBackupCodesRegeneratedEvent()
    {
        WallowUser user = WallowUser.Create(Guid.Empty, "Test", "User", "test@test.com", TimeProvider.System);
        user.EnableMfa("totp", "encrypted-secret");
        _userManager.FindByIdAsync(TestUserId).Returns(user);
        _userManager.CheckPasswordAsync(user, "correct-password").Returns(true);
        List<string> newCodes = ["newcode1", "newcode2", "newcode3"];
        _mfaService.GenerateBackupCodesAsync(Arg.Any<CancellationToken>()).Returns(newCodes);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        await _controller.RegenerateBackupCodes(
            new MfaRegenerateBackupCodesRequest("correct-password"), CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<UserMfaBackupCodesRegeneratedEvent>(e =>
                e.UserId == Guid.Parse(TestUserId) &&
                e.TenantId == _testTenantId));
    }

    #endregion
}
