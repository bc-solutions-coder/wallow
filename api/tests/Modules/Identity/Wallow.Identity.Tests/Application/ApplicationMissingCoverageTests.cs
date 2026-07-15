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

    #region ScimGroupReference

    [Fact]
    public void ScimGroupReference_DefaultsAreCorrect()
    {
        ScimGroupReference reference = new()
        {
            Value = "group-id-123"
        };

        reference.Value.Should().Be("group-id-123");
        reference.Ref.Should().BeNull();
        reference.Display.Should().BeNull();
    }

    [Fact]
    public void ScimGroupReference_AllPropertiesSet_RoundTrips()
    {
        ScimGroupReference reference = new()
        {
            Value = "group-id",
            Ref = "https://example.com/Groups/group-id",
            Display = "Engineering"
        };

        reference.Value.Should().Be("group-id");
        reference.Ref.Should().Be("https://example.com/Groups/group-id");
        reference.Display.Should().Be("Engineering");
    }

    #endregion

    #region ScimConfigurationDto

    [Fact]
    public void ScimConfigurationDto_AllPropertiesAccessible()
    {
        DateTime tokenExpiry = DateTime.UtcNow.AddDays(30);
        DateTime lastSync = DateTime.UtcNow.AddHours(-1);

        ScimConfigurationDto dto = new(
            IsEnabled: true,
            TokenPrefix: "sk-test",
            TokenExpiresAt: tokenExpiry,
            LastSyncAt: lastSync,
            ScimEndpointUrl: "https://api.example.com/scim/v2",
            AutoActivateUsers: true,
            DefaultRole: "user",
            DeprovisionOnDelete: false);

        dto.IsEnabled.Should().BeTrue();
        dto.TokenPrefix.Should().Be("sk-test");
        dto.TokenExpiresAt.Should().Be(tokenExpiry);
        dto.LastSyncAt.Should().Be(lastSync);
        dto.ScimEndpointUrl.Should().Be("https://api.example.com/scim/v2");
        dto.AutoActivateUsers.Should().BeTrue();
        dto.DefaultRole.Should().Be("user");
        dto.DeprovisionOnDelete.Should().BeFalse();
    }

    [Fact]
    public void ScimConfigurationDto_WithNullOptionals_HasNullValues()
    {
        ScimConfigurationDto dto = new(
            IsEnabled: false,
            TokenPrefix: null,
            TokenExpiresAt: null,
            LastSyncAt: null,
            ScimEndpointUrl: "https://api.example.com/scim/v2",
            AutoActivateUsers: false,
            DefaultRole: null,
            DeprovisionOnDelete: true);

        dto.IsEnabled.Should().BeFalse();
        dto.TokenPrefix.Should().BeNull();
        dto.TokenExpiresAt.Should().BeNull();
        dto.LastSyncAt.Should().BeNull();
        dto.DefaultRole.Should().BeNull();
        dto.DeprovisionOnDelete.Should().BeTrue();
    }

    #endregion

    #region ScimSyncLogDto

    [Fact]
    public void ScimSyncLogDto_AllPropertiesAccessible()
    {
        ScimSyncLogId id = new(Guid.NewGuid());
        DateTime timestamp = DateTime.UtcNow;

        ScimSyncLogDto dto = new(
            Id: id,
            Operation: ScimOperation.Create,
            ResourceType: ScimResourceType.User,
            ExternalId: "ext-123",
            InternalId: "int-456",
            Success: true,
            ErrorMessage: null,
            Timestamp: timestamp);

        dto.Id.Should().Be(id);
        dto.Operation.Should().Be(ScimOperation.Create);
        dto.ResourceType.Should().Be(ScimResourceType.User);
        dto.ExternalId.Should().Be("ext-123");
        dto.InternalId.Should().Be("int-456");
        dto.Success.Should().BeTrue();
        dto.ErrorMessage.Should().BeNull();
        dto.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void ScimSyncLogDto_WithError_HasErrorMessage()
    {
        ScimSyncLogId id = new(Guid.NewGuid());

        ScimSyncLogDto dto = new(
            Id: id,
            Operation: ScimOperation.Delete,
            ResourceType: ScimResourceType.Group,
            ExternalId: "ext-999",
            InternalId: null,
            Success: false,
            ErrorMessage: "User not found",
            Timestamp: DateTime.UtcNow);

        dto.Success.Should().BeFalse();
        dto.ErrorMessage.Should().Be("User not found");
        dto.InternalId.Should().BeNull();
    }

    #endregion

    #region ScimMeta

    [Fact]
    public void ScimMeta_AllPropertiesAccessible()
    {
        DateTime created = DateTime.UtcNow.AddDays(-5);
        DateTime modified = DateTime.UtcNow;

        ScimMeta meta = new()
        {
            ResourceType = "User",
            Created = created,
            LastModified = modified,
            Location = "https://example.com/scim/v2/Users/123",
            Version = "W/\"1\""
        };

        meta.ResourceType.Should().Be("User");
        meta.Created.Should().Be(created);
        meta.LastModified.Should().Be(modified);
        meta.Location.Should().Be("https://example.com/scim/v2/Users/123");
        meta.Version.Should().Be("W/\"1\"");
    }

    [Fact]
    public void ScimMeta_DefaultValues()
    {
        ScimMeta meta = new();

        meta.ResourceType.Should().Be(string.Empty);
        meta.Created.Should().BeNull();
        meta.LastModified.Should().BeNull();
        meta.Location.Should().BeNull();
        meta.Version.Should().BeNull();
    }

    #endregion

    #region ScimListRequest

    [Fact]
    public void ScimListRequest_DefaultValues()
    {
        ScimListRequest request = new();

        request.Filter.Should().BeNull();
        request.StartIndex.Should().Be(1);
        request.Count.Should().Be(100);
        request.SortBy.Should().BeNull();
        request.SortOrder.Should().BeNull();
    }

    [Fact]
    public void ScimListRequest_WithAllParameters()
    {
        ScimListRequest request = new(
            Filter: "userName eq \"john\"",
            StartIndex: 11,
            Count: 10,
            SortBy: "userName",
            SortOrder: "ascending");

        request.Filter.Should().Be("userName eq \"john\"");
        request.StartIndex.Should().Be(11);
        request.Count.Should().Be(10);
        request.SortBy.Should().Be("userName");
        request.SortOrder.Should().Be("ascending");
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

    #region SsoConfigurationDto

    [Fact]
    public void SsoConfigurationDto_AllPropertiesAccessible()
    {
        SsoConfigurationId id = new(Guid.NewGuid());

        SsoConfigurationDto dto = new(
            Id: id,
            DisplayName: "Okta SSO",
            Protocol: SsoProtocol.Saml,
            Status: SsoStatus.Active,
            SamlEntityId: "https://okta.example.com",
            SamlSsoUrl: "https://okta.example.com/sso/saml",
            SamlConfigured: true,
            OidcIssuer: null,
            OidcClientId: null,
            OidcConfigured: false,
            EnforceForAllUsers: false,
            AutoProvisionUsers: true,
            DefaultRole: "user",
            SyncGroupsAsRoles: false,
            ServiceProviderEntityId: "https://wallow.example.com",
            ServiceProviderAcsUrl: "https://wallow.example.com/saml/acs",
            ServiceProviderMetadataUrl: "https://wallow.example.com/saml/metadata");

        dto.Id.Should().Be(id);
        dto.DisplayName.Should().Be("Okta SSO");
        dto.Protocol.Should().Be(SsoProtocol.Saml);
        dto.Status.Should().Be(SsoStatus.Active);
        dto.SamlEntityId.Should().Be("https://okta.example.com");
        dto.SamlConfigured.Should().BeTrue();
        dto.OidcConfigured.Should().BeFalse();
        dto.AutoProvisionUsers.Should().BeTrue();
    }

    #endregion

    #region ScimUserRequest

    [Fact]
    public void ScimUserRequest_DefaultActive_IsTrue()
    {
        ScimUserRequest request = new()
        {
            UserName = "john.doe@example.com"
        };

        request.Active.Should().BeTrue();
        request.UserName.Should().Be("john.doe@example.com");
        request.ExternalId.Should().BeNull();
        request.Schemas.Should().BeNull();
    }

    [Fact]
    public void ScimUserRequest_WithAllFields()
    {
        ScimUserRequest request = new()
        {
            Schemas = ["urn:ietf:params:scim:schemas:core:2.0:User"],
            ExternalId = "ext-123",
            UserName = "john@example.com",
            DisplayName = "John Doe",
            Active = false,
            Name = new ScimName { GivenName = "John", FamilyName = "Doe" },
            Emails = [new ScimEmail { Value = "john@example.com", Primary = true }],
            Groups = [new ScimGroupReference { Value = "group-1" }]
        };

        request.Active.Should().BeFalse();
        request.ExternalId.Should().Be("ext-123");
        request.DisplayName.Should().Be("John Doe");
        request.Name!.GivenName.Should().Be("John");
    }

    #endregion

    #region ScimGroupRequest

    [Fact]
    public void ScimGroupRequest_DefaultValues()
    {
        ScimGroupRequest request = new()
        {
            DisplayName = "Engineering"
        };

        request.DisplayName.Should().Be("Engineering");
        request.ExternalId.Should().BeNull();
        request.Schemas.Should().BeNull();
        request.Members.Should().BeNull();
    }

    [Fact]
    public void ScimGroupRequest_WithMembers()
    {
        ScimGroupRequest request = new()
        {
            DisplayName = "Admins",
            ExternalId = "grp-ext-1",
            Members = [new ScimMember { Value = "user-1", Display = "Alice" }]
        };

        request.ExternalId.Should().Be("grp-ext-1");
        request.Members!.Should().HaveCount(1);
        request.Members![0].Value.Should().Be("user-1");
    }

    #endregion

    #region ScimMember

    [Fact]
    public void ScimMember_AllPropertiesAccessible()
    {
        ScimMember member = new()
        {
            Value = "user-abc",
            Ref = "https://example.com/Users/user-abc",
            Display = "Jane Smith",
            Type = "User"
        };

        member.Value.Should().Be("user-abc");
        member.Ref.Should().Be("https://example.com/Users/user-abc");
        member.Display.Should().Be("Jane Smith");
        member.Type.Should().Be("User");
    }

    #endregion

}
