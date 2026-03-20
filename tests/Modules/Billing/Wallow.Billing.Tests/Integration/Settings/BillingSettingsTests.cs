using System.Net;
using System.Net.Http.Json;
using Wallow.Billing.Api.Controllers;
using Wallow.Shared.Kernel.Settings;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Factories;

namespace Wallow.Billing.Tests.Integration.Settings;

[CollectionDefinition(nameof(BillingSettingsTestCollection))]
public class BillingSettingsTestCollection : ICollectionFixture<WallowApiFactory>;

[Collection(nameof(BillingSettingsTestCollection))]
[Trait("Category", "Integration")]
public sealed class BillingSettingsTests(WallowApiFactory factory) : WallowIntegrationTestBase(factory)
{
    private const string ConfigUrl = "/api/v1/billing/config";
    private const string TenantSettingsUrl = "/api/v1/billing/settings/tenant";

    private void SetIsolatedAdminUser()
    {
        SetTestUser(Guid.NewGuid().ToString(), "admin");
        SetTestTenant(Guid.NewGuid());
    }

    [Fact]
    public async Task GetConfig_WhenNoOverridesExist_ReturnsDefaults()
    {
        SetIsolatedAdminUser();

        HttpResponseMessage response = await Client.GetAsync(ConfigUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ResolvedSettingsConfig? config = await response.Content.ReadFromJsonAsync<ResolvedSettingsConfig>();
        config.Should().NotBeNull();
        config!.Settings.Should().ContainKey("billing.default_currency").WhoseValue.Should().Be("USD");
        config.Settings.Should().ContainKey("billing.invoice_prefix").WhoseValue.Should().Be("INV-");
    }

    [Fact]
    public async Task UpsertTenantSetting_ThenGetConfig_ReturnsTenantOverride()
    {
        SetIsolatedAdminUser();

        SettingUpdateRequest upsertRequest = new("billing.default_currency", "EUR");
        HttpResponseMessage putResponse = await Client.PutAsJsonAsync(TenantSettingsUrl, upsertRequest);
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage getResponse = await Client.GetAsync(ConfigUrl);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        ResolvedSettingsConfig? config = await getResponse.Content.ReadFromJsonAsync<ResolvedSettingsConfig>();
        config!.Settings["billing.default_currency"].Should().Be("EUR");
    }

    [Fact]
    public async Task DeleteTenantSetting_AfterUpsert_RevertsToDefault()
    {
        SetIsolatedAdminUser();

        // Upsert a tenant override
        SettingUpdateRequest upsertRequest = new("billing.invoice_prefix", "BILL-");
        await Client.PutAsJsonAsync(TenantSettingsUrl, upsertRequest);

        // Delete the override
        HttpResponseMessage deleteResponse = await Client.DeleteAsync($"{TenantSettingsUrl}?key=billing.invoice_prefix");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it reverted to default
        HttpResponseMessage getResponse = await Client.GetAsync(ConfigUrl);
        ResolvedSettingsConfig? config = await getResponse.Content.ReadFromJsonAsync<ResolvedSettingsConfig>();
        config!.Settings["billing.invoice_prefix"].Should().Be("INV-");
    }

    [Fact]
    public async Task UpsertTenantSetting_WithCustomKey_Succeeds()
    {
        SetIsolatedAdminUser();

        SettingUpdateRequest request = new("custom.billing_feature_flag", "enabled");
        HttpResponseMessage response = await Client.PutAsJsonAsync(TenantSettingsUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpsertTenantSetting_WithUnknownKey_ReturnsError()
    {
        SetIsolatedAdminUser();

        SettingUpdateRequest request = new("billing.nonexistent_key", "value");
        HttpResponseMessage response = await Client.PutAsJsonAsync(TenantSettingsUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpsertTenantSetting_WithSystemKey_ReturnsValidationError()
    {
        SetIsolatedAdminUser();

        SettingUpdateRequest request = new("system.internal_flag", "true");
        HttpResponseMessage response = await Client.PutAsJsonAsync(TenantSettingsUrl, request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetTenantSettings_WithoutBillingManagePermission_ReturnsForbidden()
    {
        SetTestUser(Guid.NewGuid().ToString(), "user");

        HttpResponseMessage response = await Client.GetAsync(TenantSettingsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpsertTenantSetting_CustomKeyLimitEnforced_WhenAtMaximum()
    {
        SetIsolatedAdminUser();

        // Create 100 custom keys to hit the limit
        for (int i = 0; i < SettingKeyValidator.MaxCustomKeysPerTenant; i++)
        {
            SettingUpdateRequest request = new($"custom.limit_test_{i:D3}", $"value_{i}");
            HttpResponseMessage putResponse = await Client.PutAsJsonAsync(TenantSettingsUrl, request);
            putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, $"custom key {i} should succeed");
        }

        // The 101st custom key should be rejected
        SettingUpdateRequest overLimitRequest = new("custom.over_the_limit", "should_fail");
        HttpResponseMessage response = await Client.PutAsJsonAsync(TenantSettingsUrl, overLimitRequest);

        response.StatusCode.Should().NotBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CacheInvalidation_AfterPut_SubsequentGetReflectsNewValue()
    {
        SetIsolatedAdminUser();

        // Get config to populate cache
        HttpResponseMessage firstGet = await Client.GetAsync(ConfigUrl);
        firstGet.StatusCode.Should().Be(HttpStatusCode.OK);
        ResolvedSettingsConfig? initialConfig = await firstGet.Content.ReadFromJsonAsync<ResolvedSettingsConfig>();
        initialConfig!.Settings["billing.date_format"].Should().Be("YYYY-MM-DD");

        // Update a setting
        SettingUpdateRequest request = new("billing.date_format", "DD/MM/YYYY");
        HttpResponseMessage putResponse = await Client.PutAsJsonAsync(TenantSettingsUrl, request);
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Get config again — should reflect the updated value, not a stale cache
        HttpResponseMessage secondGet = await Client.GetAsync(ConfigUrl);
        ResolvedSettingsConfig? updatedConfig = await secondGet.Content.ReadFromJsonAsync<ResolvedSettingsConfig>();
        updatedConfig!.Settings["billing.date_format"].Should().Be("DD/MM/YYYY");
    }
}
