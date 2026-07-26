using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Settings;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Tests.Application;

public class ApplicationMissingCoverageTests
{
    #region IdentitySettingKeys

    [Fact]
    public void IdentitySettingKeys_Timezone_HasExpectedKey()
    {
        IdentitySettingKeys.Timezone.Key.Should().Be("identity.timezone");
        IdentitySettingKeys.Timezone.DefaultValue.Should().Be("UTC");
    }

    [Fact]
    public void IdentitySettingKeys_Locale_HasExpectedKey()
    {
        IdentitySettingKeys.Locale.Key.Should().Be("identity.locale");
        IdentitySettingKeys.Locale.DefaultValue.Should().Be("en-US");
    }

    [Fact]
    public void IdentitySettingKeys_DateFormat_HasExpectedKey()
    {
        IdentitySettingKeys.DateFormat.Key.Should().Be("identity.date_format");
    }

    [Fact]
    public void IdentitySettingKeys_Theme_HasExpectedKey()
    {
        IdentitySettingKeys.Theme.Key.Should().Be("identity.theme");
        IdentitySettingKeys.Theme.DefaultValue.Should().Be("light");
    }

    [Fact]
    public void IdentitySettingKeys_ModuleName_IsIdentity()
    {
        IdentitySettingKeys keys = new();
        keys.ModuleName.Should().Be("identity");
    }

    #endregion

    #region ServiceAccountDto

    [Fact]
    public void ServiceAccountDto_AllPropertiesAccessible()
    {
        ServiceAccountMetadataId id = new(Guid.NewGuid());
        DateTimeOffset created = DateTimeOffset.UtcNow.AddDays(-10);
        DateTimeOffset lastUsed = DateTimeOffset.UtcNow.AddHours(-1);

        ServiceAccountDto dto = new(
            Id: id,
            ClientId: "sa-client-123",
            Name: "My Service Account",
            Description: "For CI/CD",
            Status: ServiceAccountStatus.Active,
            Scopes: ["invoices.read", "payments.read"],
            CreatedAt: created,
            LastUsedAt: lastUsed);

        dto.Id.Should().Be(id);
        dto.ClientId.Should().Be("sa-client-123");
        dto.Name.Should().Be("My Service Account");
        dto.Description.Should().Be("For CI/CD");
        dto.Status.Should().Be(ServiceAccountStatus.Active);
        dto.Scopes.Should().Contain("invoices.read");
        dto.CreatedAt.Should().Be(created);
        dto.LastUsedAt.Should().Be(lastUsed);
    }

    [Fact]
    public void ServiceAccountDto_WithNullOptionals()
    {
        ServiceAccountMetadataId id = new(Guid.NewGuid());

        ServiceAccountDto dto = new(
            Id: id,
            ClientId: "sa-client",
            Name: "Account",
            Description: null,
            Status: ServiceAccountStatus.Revoked,
            Scopes: [],
            CreatedAt: DateTimeOffset.UtcNow,
            LastUsedAt: null);

        dto.Description.Should().BeNull();
        dto.LastUsedAt.Should().BeNull();
        dto.Status.Should().Be(ServiceAccountStatus.Revoked);
    }

    #endregion
}
