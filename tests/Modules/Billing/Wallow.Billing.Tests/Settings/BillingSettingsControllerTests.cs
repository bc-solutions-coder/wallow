using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Billing.Api.Controllers;
using Wallow.Shared.Api.Settings;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Services;
using Wallow.Shared.Kernel.Settings;

namespace Wallow.Billing.Tests.Settings;

public class BillingSettingsControllerTests
{
    private readonly ISettingsService _settingsService;
    private readonly ISettingRegistry _settingRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly BillingSettingsController _controller;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public BillingSettingsControllerTests()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _settingRegistry = Substitute.For<ISettingRegistry>();
        _tenantContext = Substitute.For<ITenantContext>();
        _currentUserService = Substitute.For<ICurrentUserService>();

        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));
        _currentUserService.GetCurrentUserId().Returns(_userId);

        _controller = new BillingSettingsController(_settingsService, _settingRegistry, _tenantContext, _currentUserService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region GetConfig

    [Fact]
    public async Task GetConfig_WhenNoOverridesExist_ReturnsCodeDefaults()
    {
        Dictionary<string, string> defaults = new()
        {
            ["billing.currency"] = "USD",
            ["billing.tax_rate"] = "0.0"
        };
        ResolvedSettingsConfig config = new(defaults);
        _settingsService.GetConfigAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(config);

        IActionResult result = await _controller.GetConfig(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ResolvedSettingsConfig returned = ok.Value.Should().BeOfType<ResolvedSettingsConfig>().Subject;
        returned.Settings.Should().ContainKey("billing.currency").WhoseValue.Should().Be("USD");
        returned.Settings.Should().ContainKey("billing.tax_rate").WhoseValue.Should().Be("0.0");
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
    public async Task GetTenantSettings_WhenNoOverridesExist_ReturnsEmptyList()
    {
        _settingsService.GetTenantSettingsAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ResolvedSetting>());

        IActionResult result = await _controller.GetTenantSettings(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ResolvedSetting> settings = ok.Value.Should().BeAssignableTo<IReadOnlyList<ResolvedSetting>>().Subject;
        settings.Should().BeEmpty();
    }

    [Fact]
    public void GetTenantSettings_RequiresBillingManagePermission()
    {
        MethodInfo method = typeof(BillingSettingsController).GetMethod(nameof(BillingSettingsController.GetTenantSettings))!;
        HasPermissionAttribute attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;

        attribute.Should().NotBeNull();
        attribute.Permission.Should().Be(PermissionType.BillingManage);
    }

    #endregion

    #region GetUserSettings

    [Fact]
    public async Task GetUserSettings_WhenNoOverridesExist_ReturnsEmptyList()
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

    #region Override / Cascade

    [Fact]
    public async Task GetConfig_AfterTenantSettingUpserted_ReturnsTenantValueOverridingDefault()
    {
        Dictionary<string, string> configWithTenantOverride = new()
        {
            ["billing.currency"] = "EUR",
            ["billing.tax_rate"] = "0.0"
        };
        ResolvedSettingsConfig config = new(configWithTenantOverride);
        _settingsService.GetConfigAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(config);

        IActionResult result = await _controller.GetConfig(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ResolvedSettingsConfig returned = ok.Value.Should().BeOfType<ResolvedSettingsConfig>().Subject;
        returned.Settings.Should().ContainKey("billing.currency").WhoseValue.Should().Be("EUR");
    }

    [Fact]
    public async Task GetConfig_AfterUserSettingUpserted_ReturnsUserValueOverridingTenant()
    {
        Dictionary<string, string> configWithUserOverride = new()
        {
            ["billing.currency"] = "GBP",
            ["billing.tax_rate"] = "0.0"
        };
        ResolvedSettingsConfig config = new(configWithUserOverride);
        _settingsService.GetConfigAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(config);

        IActionResult result = await _controller.GetConfig(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ResolvedSettingsConfig returned = ok.Value.Should().BeOfType<ResolvedSettingsConfig>().Subject;
        returned.Settings.Should().ContainKey("billing.currency").WhoseValue.Should().Be("GBP");
    }

    [Fact]
    public async Task GetConfig_AfterUserSettingDeleted_FallsBackToTenantValue()
    {
        Dictionary<string, string> configAfterUserDelete = new()
        {
            ["billing.currency"] = "EUR",
            ["billing.tax_rate"] = "0.0"
        };
        ResolvedSettingsConfig config = new(configAfterUserDelete);
        _settingsService.GetConfigAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(config);

        IActionResult result = await _controller.GetConfig(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ResolvedSettingsConfig returned = ok.Value.Should().BeOfType<ResolvedSettingsConfig>().Subject;
        returned.Settings.Should().ContainKey("billing.currency").WhoseValue.Should().Be("EUR");
    }

    [Fact]
    public async Task GetConfig_AfterTenantSettingDeleted_FallsBackToCodeDefault()
    {
        Dictionary<string, string> configWithDefaults = new()
        {
            ["billing.currency"] = "USD",
            ["billing.tax_rate"] = "0.0"
        };
        ResolvedSettingsConfig config = new(configWithDefaults);
        _settingsService.GetConfigAsync(_tenantId, _userId, Arg.Any<CancellationToken>())
            .Returns(config);

        IActionResult result = await _controller.GetConfig(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ResolvedSettingsConfig returned = ok.Value.Should().BeOfType<ResolvedSettingsConfig>().Subject;
        returned.Settings.Should().ContainKey("billing.currency").WhoseValue.Should().Be("USD");
    }

    [Fact]
    public async Task UpsertTenantSetting_WithCustomKey_ReturnsNoContent()
    {
        _settingRegistry.IsCodeDefinedKey("custom.my_feature").Returns(false);

        SettingUpdateRequest request = new("custom.my_feature", "enabled");
        IActionResult result = await _controller.UpsertTenantSetting(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _settingsService.Received(1).UpdateTenantSettingsAsync(
            _tenantId,
            Arg.Is<List<SettingUpdate>>(u => u[0].Key == "custom.my_feature" && u[0].Value == "enabled"),
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

    #endregion
}
