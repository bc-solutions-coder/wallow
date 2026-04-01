using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Shared.Kernel.Tests.Identity;

public class ScopePermissionMapperTests
{
    // Identity - Users

    [Fact]
    public void MapScopeToPermission_UsersRead_ReturnsUsersRead()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("users.read");

        result.Should().Be(PermissionType.UsersRead);
    }

    [Fact]
    public void MapScopeToPermission_UsersWrite_ReturnsUsersUpdate()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("users.write");

        result.Should().Be(PermissionType.UsersUpdate);
    }

    [Fact]
    public void MapScopeToPermission_UsersManage_ReturnsUsersDelete()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("users.manage");

        result.Should().Be(PermissionType.UsersDelete);
    }

    // Identity - Roles

    [Fact]
    public void MapScopeToPermission_RolesRead_ReturnsRolesRead()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("roles.read");

        result.Should().Be(PermissionType.RolesRead);
    }

    [Fact]
    public void MapScopeToPermission_RolesWrite_ReturnsRolesUpdate()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("roles.write");

        result.Should().Be(PermissionType.RolesUpdate);
    }

    [Fact]
    public void MapScopeToPermission_RolesManage_ReturnsRolesDelete()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("roles.manage");

        result.Should().Be(PermissionType.RolesDelete);
    }

    // Identity - Organizations

    [Fact]
    public void MapScopeToPermission_OrganizationsRead_ReturnsOrganizationsRead()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("organizations.read");

        result.Should().Be(PermissionType.OrganizationsRead);
    }

    [Fact]
    public void MapScopeToPermission_OrganizationsWrite_ReturnsOrganizationsUpdate()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("organizations.write");

        result.Should().Be(PermissionType.OrganizationsUpdate);
    }

    [Fact]
    public void MapScopeToPermission_OrganizationsManage_ReturnsOrganizationsManageMembers()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("organizations.manage");

        result.Should().Be(PermissionType.OrganizationsManageMembers);
    }

    // Identity - API Keys

    [Fact]
    public void MapScopeToPermission_ApikeysRead_ReturnsApiKeysRead()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("apikeys.read");

        result.Should().Be(PermissionType.ApiKeysRead);
    }

    [Fact]
    public void MapScopeToPermission_ApikeysWrite_ReturnsApiKeysUpdate()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("apikeys.write");

        result.Should().Be(PermissionType.ApiKeysUpdate);
    }

    [Fact]
    public void MapScopeToPermission_ApikeysManage_ReturnsApiKeyManage()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("apikeys.manage");

        result.Should().Be(PermissionType.ApiKeyManage);
    }

    // Identity - SSO/SCIM

    [Fact]
    public void MapScopeToPermission_SsoRead_ReturnsSsoRead()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("sso.read");

        result.Should().Be(PermissionType.SsoRead);
    }

    [Fact]
    public void MapScopeToPermission_SsoManage_ReturnsSsoManage()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("sso.manage");

        result.Should().Be(PermissionType.SsoManage);
    }

    [Fact]
    public void MapScopeToPermission_ScimManage_ReturnsScimManage()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("scim.manage");

        result.Should().Be(PermissionType.ScimManage);
    }

    // Storage

    [Fact]
    public void MapScopeToPermission_StorageRead_ReturnsStorageRead()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("storage.read");

        result.Should().Be(PermissionType.StorageRead);
    }

    [Fact]
    public void MapScopeToPermission_StorageWrite_ReturnsStorageWrite()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("storage.write");

        result.Should().Be(PermissionType.StorageWrite);
    }

    // Communications

    [Fact]
    public void MapScopeToPermission_MessagingAccess_ReturnsMessagingAccess()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("messaging.access");

        result.Should().Be(PermissionType.MessagingAccess);
    }

    [Fact]
    public void MapScopeToPermission_AnnouncementsRead_ReturnsAnnouncementRead()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("announcements.read");

        result.Should().Be(PermissionType.AnnouncementRead);
    }

    [Fact]
    public void MapScopeToPermission_AnnouncementsManage_ReturnsAnnouncementManage()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("announcements.manage");

        result.Should().Be(PermissionType.AnnouncementManage);
    }

    [Fact]
    public void MapScopeToPermission_ChangelogManage_ReturnsChangelogManage()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("changelog.manage");

        result.Should().Be(PermissionType.ChangelogManage);
    }

    [Fact]
    public void MapScopeToPermission_NotificationsRead_ReturnsNotificationRead()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("notifications.read");

        result.Should().Be(PermissionType.NotificationRead);
    }

    [Fact]
    public void MapScopeToPermission_NotificationsWrite_ReturnsNotificationsWrite()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("notifications.write");

        result.Should().Be(PermissionType.NotificationsWrite);
    }

    // Configuration

    [Fact]
    public void MapScopeToPermission_ConfigurationRead_ReturnsConfigurationRead()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("configuration.read");

        result.Should().Be(PermissionType.ConfigurationRead);
    }

    [Fact]
    public void MapScopeToPermission_ConfigurationManage_ReturnsConfigurationManage()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("configuration.manage");

        result.Should().Be(PermissionType.ConfigurationManage);
    }

    // Inquiries

    [Fact]
    public void MapScopeToPermission_InquiriesRead_ReturnsInquiriesRead()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("inquiries.read");

        result.Should().Be(PermissionType.InquiriesRead);
    }

    [Fact]
    public void MapScopeToPermission_InquiriesWrite_ReturnsInquiriesWrite()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("inquiries.write");

        result.Should().Be(PermissionType.InquiriesWrite);
    }

    // Identity - Service Accounts

    [Fact]
    public void MapScopeToPermission_ServiceaccountsRead_ReturnsServiceAccountsRead()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("serviceaccounts.read");

        result.Should().Be(PermissionType.ServiceAccountsRead);
    }

    [Fact]
    public void MapScopeToPermission_ServiceaccountsWrite_ReturnsServiceAccountsWrite()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("serviceaccounts.write");

        result.Should().Be(PermissionType.ServiceAccountsWrite);
    }

    [Fact]
    public void MapScopeToPermission_ServiceaccountsManage_ReturnsServiceAccountsManage()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("serviceaccounts.manage");

        result.Should().Be(PermissionType.ServiceAccountsManage);
    }

    // Platform

    [Fact]
    public void MapScopeToPermission_WebhooksManage_ReturnsWebhooksManage()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("webhooks.manage");

        result.Should().Be(PermissionType.WebhooksManage);
    }

    // Unknown scopes

    [Fact]
    public void MapScopeToPermission_UnknownScope_ReturnsNull()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("unknown.scope");

        result.Should().BeNull();
    }

    [Fact]
    public void MapScopeToPermission_EmptyString_ReturnsNull()
    {
        string? result = ScopePermissionMapper.MapScopeToPermission("");

        result.Should().BeNull();
    }
}
