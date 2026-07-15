using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Api.Controllers;
using Wallow.Shared.Api.Settings;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Services;
using Wallow.Shared.Kernel.Settings;

namespace Wallow.Identity.Tests.Api.Controllers;

public class IdentitySettingsControllerTests
{
    private static readonly Guid _tenantGuid = Guid.NewGuid();
    private static readonly Guid _userGuid = Guid.NewGuid();

    private readonly ISettingsService _settingsService;
    private readonly ISettingRegistry _settingRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IdentitySettingsController _controller;

    public IdentitySettingsControllerTests()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _settingRegistry = Substitute.For<ISettingRegistry>();
        _tenantContext = Substitute.For<ITenantContext>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        _tenantContext.TenantId.Returns(new TenantId(_tenantGuid));
        _currentUserService.GetCurrentUserId().Returns(_userGuid);

        // IdentitySettingsController uses [FromKeyedServices], we must use a service provider
        ServiceCollection services = new ServiceCollection();
        services.AddKeyedSingleton("identity", _settingsService);
        services.AddKeyedSingleton("identity", _settingRegistry);
        ServiceProvider provider = services.BuildServiceProvider();

        _controller = new IdentitySettingsController(
            provider.GetRequiredKeyedService<ISettingsService>("identity"),
            provider.GetRequiredKeyedService<ISettingRegistry>("identity"),
            _tenantContext,
            _currentUserService);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _userGuid.ToString()),
                }, "TestAuth"))
            }
        };
    }

    #region GetConfig

    [Fact]
    public async Task GetConfig_WithValidUser_ReturnsOk()
    {
        ResolvedSettingsConfig config = new(new Dictionary<string, string> { ["identity.timezone"] = "UTC" });
        _settingsService.GetConfigAsync(_tenantGuid, _userGuid, Arg.Any<CancellationToken>())
            .Returns(config);

        IActionResult result = await _controller.GetConfig(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetConfig_WhenNoUser_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult result = await _controller.GetConfig(CancellationToken.None);

        ObjectResult problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region GetTenantSettings

    [Fact]
    public async Task GetTenantSettings_ReturnsOkWithSettings()
    {
        List<ResolvedSetting> settings =
        [
            new("identity.timezone", "UTC", "default", "Timezone", "The timezone", "UTC")
        ];
        _settingsService.GetTenantSettingsAsync(_tenantGuid, Arg.Any<CancellationToken>())
            .Returns(settings);

        IActionResult result = await _controller.GetTenantSettings(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetUserSettings

    [Fact]
    public async Task GetUserSettings_WithValidUser_ReturnsOk()
    {
        List<ResolvedSetting> settings = [];
        _settingsService.GetUserSettingsAsync(_tenantGuid, _userGuid, Arg.Any<CancellationToken>())
            .Returns(settings);

        IActionResult result = await _controller.GetUserSettings(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUserSettings_WhenNoUser_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult result = await _controller.GetUserSettings(CancellationToken.None);

        ObjectResult problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region UpsertTenantSetting

    [Fact]
    public async Task UpsertTenantSetting_WithValidKey_ReturnsNoContent()
    {
        SettingUpdateRequest request = new("identity.timezone", "America/New_York");
        _settingRegistry.IsCodeDefinedKey("identity.timezone").Returns(true);

        IActionResult result = await _controller.UpsertTenantSetting(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _settingsService.Received(1).UpdateTenantSettingsAsync(
            _tenantGuid,
            Arg.Any<IReadOnlyList<SettingUpdate>>(),
            _userGuid,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertTenantSetting_WithSystemKey_ReturnsValidationError()
    {
        SettingUpdateRequest request = new("system.some.key", "value");
        // system key - SettingKeyValidator returns System

        IActionResult result = await _controller.UpsertTenantSetting(request, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which
            .StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task UpsertTenantSetting_WithUnknownKey_ReturnsValidationError()
    {
        SettingUpdateRequest request = new("unknown.key", "value");
        _settingRegistry.IsCodeDefinedKey("unknown.key").Returns(false);

        IActionResult result = await _controller.UpsertTenantSetting(request, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which
            .StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task UpsertTenantSetting_WhenNoUser_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        SettingUpdateRequest request = new("custom.key", "value");

        IActionResult result = await _controller.UpsertTenantSetting(request, CancellationToken.None);

        ObjectResult problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region DeleteTenantSetting

    [Fact]
    public async Task DeleteTenantSetting_WithValidKey_ReturnsNoContent()
    {
        _settingRegistry.IsCodeDefinedKey("identity.timezone").Returns(true);

        IActionResult result = await _controller.DeleteTenantSetting("identity.timezone", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _settingsService.Received(1).DeleteTenantSettingsAsync(
            _tenantGuid,
            Arg.Any<IReadOnlyList<string>>(),
            _userGuid,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTenantSetting_WithSystemKey_ReturnsValidationError()
    {
        IActionResult result = await _controller.DeleteTenantSetting("system.key", CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which
            .StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task DeleteTenantSetting_WhenNoUser_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        _settingRegistry.IsCodeDefinedKey("custom.key").Returns(false);

        IActionResult result = await _controller.DeleteTenantSetting("custom.key", CancellationToken.None);

        ObjectResult problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region UpsertUserSetting

    [Fact]
    public async Task UpsertUserSetting_WithValidKey_ReturnsNoContent()
    {
        SettingUpdateRequest request = new("identity.theme", "dark");
        _settingRegistry.IsCodeDefinedKey("identity.theme").Returns(true);

        IActionResult result = await _controller.UpsertUserSetting(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _settingsService.Received(1).UpdateUserSettingsAsync(
            _tenantGuid,
            _userGuid,
            Arg.Any<IReadOnlyList<SettingUpdate>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertUserSetting_WithSystemKey_ReturnsValidationError()
    {
        SettingUpdateRequest request = new("system.internal", "value");

        IActionResult result = await _controller.UpsertUserSetting(request, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which
            .StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task UpsertUserSetting_WhenNoUser_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        SettingUpdateRequest request = new("custom.key", "value");

        IActionResult result = await _controller.UpsertUserSetting(request, CancellationToken.None);

        ObjectResult problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region DeleteUserSetting

    [Fact]
    public async Task DeleteUserSetting_WithValidKey_ReturnsNoContent()
    {
        _settingRegistry.IsCodeDefinedKey("identity.timezone").Returns(true);

        IActionResult result = await _controller.DeleteUserSetting("identity.timezone", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _settingsService.Received(1).DeleteUserSettingsAsync(
            _tenantGuid,
            _userGuid,
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserSetting_WithSystemKey_ReturnsValidationError()
    {
        IActionResult result = await _controller.DeleteUserSetting("system.key", CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which
            .StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task DeleteUserSetting_WhenNoUser_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        _settingRegistry.IsCodeDefinedKey("custom.key").Returns(false);

        IActionResult result = await _controller.DeleteUserSetting("custom.key", CancellationToken.None);

        ObjectResult problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion
}
