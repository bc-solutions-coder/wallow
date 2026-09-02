using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Kernel.Settings;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Identity.IntegrationTests.Settings;

/// <summary>
/// Wallow-dvbc: <c>IdentitySettingsController</c> injects <c>[FromKeyedServices("identity")]</c>
/// <see cref="ISettingsService"/> and <see cref="ISettingRegistry"/>, but nothing ever registers
/// those keyed services. <c>AddSettings&lt;TDbContext, TRegistry&gt;</c> constrains
/// <c>TDbContext</c> to <c>TenantAwareDbContext</c>, which <c>IdentityDbContext</c> (ASP.NET
/// Identity's <c>IdentityDbContext</c>) cannot satisfy, so Identity skips the registration
/// entirely. Controller activation therefore throws and every identity settings endpoint 500s.
///
/// These tests go through the real Program/DI container so they reproduce the production
/// failure. The existing unit test <c>IdentitySettingsControllerTests</c> does NOT cover this —
/// it hand-registers the keyed services in its own ServiceCollection, i.e. it fakes the exact
/// registration that is missing.
///
/// Backend-dependent: requires the WallowApiFactory stack (Postgres + seeded identity data).
/// </summary>
[Trait("Category", "Integration")]
public class IdentitySettingsResolutionTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    /// <summary>
    /// Root cause, asserted directly against the real container: the keyed 'identity'
    /// registrations must exist. Returns null today.
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
    /// GET leg (tenant scope). The 'admin' role expands to SystemSettings via
    /// PermissionExpansionMiddleware, so authorization passes and the request reaches controller
    /// activation — which is where the missing keyed service surfaces as a 500.
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
    /// GET leg (user scope). This endpoint has no permission gate, so it isolates the DI
    /// activation failure from any authorization concern.
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
    /// PUT leg. An unregistered key must be rejected by the registry as a 400 validation
    /// failure (<c>Settings.UnknownKey</c>) — reaching that check proves both keyed
    /// services resolved rather than failing with a 500 before any validation runs.
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
