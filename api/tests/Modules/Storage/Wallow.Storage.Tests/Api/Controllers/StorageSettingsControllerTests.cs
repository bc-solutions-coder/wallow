using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Shared.Api.Settings;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Services;
using Wallow.Shared.Kernel.Settings;
using Wallow.Storage.Api.Controllers;

namespace Wallow.Storage.Tests.Api.Controllers;

public class StorageSettingsControllerTests
{
    private readonly ISettingsService _settingsService;
    private readonly ISettingRegistry _settingRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly StorageSettingsController _controller;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public StorageSettingsControllerTests()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _settingRegistry = Substitute.For<ISettingRegistry>();
        _tenantContext = Substitute.For<ITenantContext>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));
        _currentUserService.GetCurrentUserId().Returns(_userId);

        _controller = new StorageSettingsController(_settingsService, _settingRegistry, _tenantContext, _currentUserService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region GetConfig

    [Fact]
    public async Task GetConfig_WhenUserExists_ReturnsOkWithConfig()
    {
        Dictionary<string, string> settings = new()
        {
            ["storage.max_upload_size_mb"] = "50",
            ["storage.allowed_file_types"] = "jpg,png,pdf"
        };
        ResolvedSettingsConfig config = new(settings);
        _settingsService.GetConfigAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(config);

        IActionResult result = await _controller.GetConfig(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ResolvedSettingsConfig returned = ok.Value.Should().BeOfType<ResolvedSettingsConfig>().Subject;
        returned.Settings.Should().ContainKey("storage.max_upload_size_mb").WhoseValue.Should().Be("50");
    }

    [Fact]
    public async Task GetConfig_WhenUserIdIsNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult result = await _controller.GetConfig(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region GetTenantSettings

    [Fact]
    public async Task GetTenantSettings_WhenNoOverrides_ReturnsEmptyList()
    {
        _settingsService.GetTenantSettingsAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ResolvedSetting>());

        IActionResult result = await _controller.GetTenantSettings(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ResolvedSetting> settings = ok.Value.Should().BeAssignableTo<IReadOnlyList<ResolvedSetting>>().Subject;
        settings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantSettings_WithOverrides_ReturnsSettingsList()
    {
        ResolvedSetting[] resolvedSettings =
        [
            new ResolvedSetting("storage.max_upload_size_mb", "100", "tenant", null, null, null)
        ];
        _settingsService.GetTenantSettingsAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(resolvedSettings);

        IActionResult result = await _controller.GetTenantSettings(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ResolvedSetting> settings = ok.Value.Should().BeAssignableTo<IReadOnlyList<ResolvedSetting>>().Subject;
        settings.Should().HaveCount(1);
    }

    [Fact]
    public void GetTenantSettings_RequiresStorageWritePermission()
    {
        MethodInfo method = typeof(StorageSettingsController).GetMethod(nameof(StorageSettingsController.GetTenantSettings))!;
        HasPermissionAttribute attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;

        attribute.Should().NotBeNull();
        attribute.Permission.Should().Be(PermissionType.StorageWrite);
    }

    #endregion

    #region GetUserSettings

    [Fact]
    public async Task GetUserSettings_WhenNoOverrides_ReturnsEmptyList()
    {
        _settingsService.GetUserSettingsAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ResolvedSetting>());

        IActionResult result = await _controller.GetUserSettings(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ResolvedSetting> settings = ok.Value.Should().BeAssignableTo<IReadOnlyList<ResolvedSetting>>().Subject;
        settings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserSettings_WhenUserIdIsNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);

        IActionResult result = await _controller.GetUserSettings(CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region UpsertTenantSetting

    [Fact]
    public async Task UpsertTenantSetting_WithCodeDefinedKey_ReturnsNoContent()
    {
        _settingRegistry.IsCodeDefinedKey("storage.max_upload_size_mb").Returns(true);
        SettingUpdateRequest request = new("storage.max_upload_size_mb", "100");

        IActionResult result = await _controller.UpsertTenantSetting(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _settingsService.Received(1).UpdateTenantSettingsAsync(
            _tenantId,
            Arg.Is<List<SettingUpdate>>(u => u[0].Key == "storage.max_upload_size_mb" && u[0].Value == "100"),
            _userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertTenantSetting_WithSystemKey_ReturnsValidationError()
    {
        _settingRegistry.IsCodeDefinedKey("system.internal_flag").Returns(false);
        SettingUpdateRequest request = new("system.internal_flag", "true");

        IActionResult result = await _controller.UpsertTenantSetting(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task UpsertTenantSetting_WithUnknownKey_ReturnsValidationError()
    {
        _settingRegistry.IsCodeDefinedKey("unknown.key").Returns(false);
        SettingUpdateRequest request = new("unknown.key", "value");

        IActionResult result = await _controller.UpsertTenantSetting(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task UpsertTenantSetting_WhenUserIdIsNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        _settingRegistry.IsCodeDefinedKey("storage.max_upload_size_mb").Returns(true);
        SettingUpdateRequest request = new("storage.max_upload_size_mb", "100");

        IActionResult result = await _controller.UpsertTenantSetting(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region DeleteTenantSetting

    [Fact]
    public async Task DeleteTenantSetting_WithCodeDefinedKey_ReturnsNoContent()
    {
        _settingRegistry.IsCodeDefinedKey("storage.max_upload_size_mb").Returns(true);

        IActionResult result = await _controller.DeleteTenantSetting("storage.max_upload_size_mb", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _settingsService.Received(1).DeleteTenantSettingsAsync(
            _tenantId,
            Arg.Is<List<string>>(keys => keys[0] == "storage.max_upload_size_mb"),
            _userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTenantSetting_WithSystemKey_ReturnsValidationError()
    {
        _settingRegistry.IsCodeDefinedKey("system.internal_flag").Returns(false);

        IActionResult result = await _controller.DeleteTenantSetting("system.internal_flag", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task DeleteTenantSetting_WhenUserIdIsNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        _settingRegistry.IsCodeDefinedKey("storage.max_upload_size_mb").Returns(true);

        IActionResult result = await _controller.DeleteTenantSetting("storage.max_upload_size_mb", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region UpsertUserSetting

    [Fact]
    public async Task UpsertUserSetting_WithCodeDefinedKey_ReturnsNoContent()
    {
        _settingRegistry.IsCodeDefinedKey("storage.allowed_file_types").Returns(true);
        SettingUpdateRequest request = new("storage.allowed_file_types", "jpg,png");

        IActionResult result = await _controller.UpsertUserSetting(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _settingsService.Received(1).UpdateUserSettingsAsync(
            _tenantId,
            _userId,
            Arg.Is<List<SettingUpdate>>(u => u[0].Key == "storage.allowed_file_types" && u[0].Value == "jpg,png"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertUserSetting_WithSystemKey_ReturnsValidationError()
    {
        _settingRegistry.IsCodeDefinedKey("system.flag").Returns(false);
        SettingUpdateRequest request = new("system.flag", "true");

        IActionResult result = await _controller.UpsertUserSetting(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task UpsertUserSetting_WhenUserIdIsNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        _settingRegistry.IsCodeDefinedKey("storage.allowed_file_types").Returns(true);
        SettingUpdateRequest request = new("storage.allowed_file_types", "jpg");

        IActionResult result = await _controller.UpsertUserSetting(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion

    #region DeleteUserSetting

    [Fact]
    public async Task DeleteUserSetting_WithCodeDefinedKey_ReturnsNoContent()
    {
        _settingRegistry.IsCodeDefinedKey("storage.allowed_file_types").Returns(true);

        IActionResult result = await _controller.DeleteUserSetting("storage.allowed_file_types", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _settingsService.Received(1).DeleteUserSettingsAsync(
            _tenantId,
            _userId,
            Arg.Is<List<string>>(keys => keys[0] == "storage.allowed_file_types"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserSetting_WithSystemKey_ReturnsValidationError()
    {
        _settingRegistry.IsCodeDefinedKey("system.flag").Returns(false);

        IActionResult result = await _controller.DeleteUserSetting("system.flag", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task DeleteUserSetting_WhenUserIdIsNull_Returns401()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        _settingRegistry.IsCodeDefinedKey("storage.allowed_file_types").Returns(true);

        IActionResult result = await _controller.DeleteUserSetting("storage.allowed_file_types", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #endregion
}
