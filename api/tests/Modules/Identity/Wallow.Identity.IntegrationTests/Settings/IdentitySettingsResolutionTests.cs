using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Kernel.Settings;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Identity.IntegrationTests.Settings;

/// <summary>
/// Checks keyed <see cref="ISettingsService"/> and <see cref="ISettingRegistry"/> resolution
/// and settings endpoints through the host container.
/// </summary>
[Trait("Category", "Integration")]
public class IdentitySettingsResolutionTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    /// <summary>
    /// Checks that both identity-keyed settings services resolve.
    /// </summary>
    [Fact]
    public void RealContainer_ResolvesKeyedIdentitySettingsServices()
    {
        ISettingsService? settingsService = ScopedServices.GetKeyedService<ISettingsService>("identity");
        ISettingRegistry? settingRegistry = ScopedServices.GetKeyedService<ISettingRegistry>("identity");

        settingsService.Should().NotBeNull("IdentitySettingsController resolves ISettingsService with key 'identity'");
        settingRegistry.Should().NotBeNull("IdentitySettingsController resolves ISettingRegistry with key 'identity'");
    }

    /// <summary>
    /// Checks that an admin request can read tenant settings.
    /// </summary>
    [Fact]
    public async Task GetTenantSettings_AsAdmin_ReturnsOk()
    {
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");

        HttpResponseMessage response = await Client.GetAsync("/identity/settings/tenant");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<ResolvedSetting>? settings =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<ResolvedSetting>>();
        settings.Should().NotBeNull();
    }

    /// <summary>
    /// Checks that an authenticated request can read user settings.
    /// </summary>
    [Fact]
    public async Task GetUserSettings_AsAdmin_ReturnsOk()
    {
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");

        HttpResponseMessage response = await Client.GetAsync("/identity/settings/user");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<ResolvedSetting>? settings =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<ResolvedSetting>>();
        settings.Should().NotBeNull();
    }

    /// <summary>
    /// Checks a bad-request response for an unknown tenant setting key.
    /// </summary>
    [Fact]
    public async Task PutTenantSetting_WithUnknownKey_ReturnsBadRequest()
    {
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");

        object request = new { key = "wallow.dvbc.unknown.key", value = "any" };
        HttpResponseMessage response = await Client.PutAsJsonAsync("/identity/settings/tenant", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
