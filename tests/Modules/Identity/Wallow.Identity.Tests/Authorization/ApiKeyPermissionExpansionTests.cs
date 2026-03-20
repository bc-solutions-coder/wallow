using System.Security.Claims;
using Wallow.Identity.Application.Constants;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Http;

namespace Wallow.Identity.Tests.Authorization;

public class ApiKeyPermissionExpansionTests
{
    [Fact]
    public async Task InvokeAsync_WithApiKeyAuthMethod_ExpandsScopesToPermissions()
    {
        Claim[] claims = new[]
        {
            new Claim("auth_method", "api_key"),
            new Claim("scope", "invoices.read invoices.write")
        };

        ClaimsIdentity identity = new(claims, "ApiKey");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.InvoicesRead);
        permissions.Should().Contain(PermissionType.InvoicesWrite);
    }

    [Fact]
    public async Task InvokeAsync_WithApiKeyAuthMethod_IgnoresUnknownScopes()
    {
        Claim[] claims = new[]
        {
            new Claim("auth_method", "api_key"),
            new Claim("scope", "unknown.scope invoices.read bogus.permission")
        };

        ClaimsIdentity identity = new(claims, "ApiKey");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().ContainSingle();
        permissions.Should().Contain(PermissionType.InvoicesRead);
    }

    [Fact]
    public async Task InvokeAsync_WithApiKeyAuthMethod_ExpandsAllScopeTypes()
    {
        Claim[] claims = new[]
        {
            new Claim("auth_method", "api_key"),
            new Claim("scope", "invoices.read payments.write users.read notifications.write webhooks.manage")
        };

        ClaimsIdentity identity = new(claims, "ApiKey");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().HaveCount(5);
        permissions.Should().Contain(PermissionType.InvoicesRead);
        permissions.Should().Contain(PermissionType.PaymentsWrite);
        permissions.Should().Contain(PermissionType.UsersRead);
        permissions.Should().Contain(PermissionType.NotificationsWrite);
        permissions.Should().Contain(PermissionType.WebhooksManage);
    }

    [Fact]
    public void ValidScopes_ContainsExpectedCount()
    {
        ApiScopes.ValidScopes.Should().HaveCount(41);
    }

    [Theory]
    [InlineData("billing.read")]
    [InlineData("billing.manage")]
    [InlineData("invoices.read")]
    [InlineData("invoices.write")]
    [InlineData("payments.read")]
    [InlineData("payments.write")]
    [InlineData("subscriptions.read")]
    [InlineData("subscriptions.write")]
    [InlineData("users.read")]
    [InlineData("users.write")]
    [InlineData("users.manage")]
    [InlineData("roles.read")]
    [InlineData("roles.write")]
    [InlineData("roles.manage")]
    [InlineData("organizations.read")]
    [InlineData("organizations.write")]
    [InlineData("organizations.manage")]
    [InlineData("apikeys.read")]
    [InlineData("apikeys.write")]
    [InlineData("apikeys.manage")]
    [InlineData("sso.read")]
    [InlineData("sso.manage")]
    [InlineData("scim.manage")]
    [InlineData("storage.read")]
    [InlineData("storage.write")]
    [InlineData("messaging.access")]
    [InlineData("announcements.read")]
    [InlineData("announcements.manage")]
    [InlineData("changelog.manage")]
    [InlineData("notifications.read")]
    [InlineData("notifications.write")]
    [InlineData("configuration.read")]
    [InlineData("configuration.manage")]
    [InlineData("showcases.read")]
    [InlineData("showcases.manage")]
    [InlineData("inquiries.read")]
    [InlineData("inquiries.write")]
    [InlineData("serviceaccounts.read")]
    [InlineData("serviceaccounts.write")]
    [InlineData("serviceaccounts.manage")]
    [InlineData("webhooks.manage")]
    public void ValidScopes_ContainsExpectedScope(string scope)
    {
        ApiScopes.ValidScopes.Should().Contain(scope);
    }

    [Theory]
    [InlineData("billing.read", PermissionType.BillingRead)]
    [InlineData("billing.manage", PermissionType.BillingManage)]
    [InlineData("invoices.read", PermissionType.InvoicesRead)]
    [InlineData("invoices.write", PermissionType.InvoicesWrite)]
    [InlineData("payments.read", PermissionType.PaymentsRead)]
    [InlineData("payments.write", PermissionType.PaymentsWrite)]
    [InlineData("subscriptions.read", PermissionType.SubscriptionsRead)]
    [InlineData("subscriptions.write", PermissionType.SubscriptionsWrite)]
    [InlineData("users.read", PermissionType.UsersRead)]
    [InlineData("users.write", PermissionType.UsersUpdate)]
    [InlineData("users.manage", PermissionType.UsersDelete)]
    [InlineData("roles.read", PermissionType.RolesRead)]
    [InlineData("roles.write", PermissionType.RolesUpdate)]
    [InlineData("roles.manage", PermissionType.RolesDelete)]
    [InlineData("organizations.read", PermissionType.OrganizationsRead)]
    [InlineData("organizations.write", PermissionType.OrganizationsUpdate)]
    [InlineData("organizations.manage", PermissionType.OrganizationsManageMembers)]
    [InlineData("apikeys.read", PermissionType.ApiKeysRead)]
    [InlineData("apikeys.write", PermissionType.ApiKeysUpdate)]
    [InlineData("apikeys.manage", PermissionType.ApiKeyManage)]
    [InlineData("sso.read", PermissionType.SsoRead)]
    [InlineData("sso.manage", PermissionType.SsoManage)]
    [InlineData("scim.manage", PermissionType.ScimManage)]
    [InlineData("storage.read", PermissionType.StorageRead)]
    [InlineData("storage.write", PermissionType.StorageWrite)]
    [InlineData("messaging.access", PermissionType.MessagingAccess)]
    [InlineData("announcements.read", PermissionType.AnnouncementRead)]
    [InlineData("announcements.manage", PermissionType.AnnouncementManage)]
    [InlineData("changelog.manage", PermissionType.ChangelogManage)]
    [InlineData("notifications.read", PermissionType.NotificationsRead)]
    [InlineData("notifications.write", PermissionType.NotificationsWrite)]
    [InlineData("configuration.read", PermissionType.ConfigurationRead)]
    [InlineData("configuration.manage", PermissionType.ConfigurationManage)]
    [InlineData("showcases.read", PermissionType.ShowcasesRead)]
    [InlineData("showcases.manage", PermissionType.ShowcasesManage)]
    [InlineData("inquiries.read", PermissionType.InquiriesRead)]
    [InlineData("inquiries.write", PermissionType.InquiriesWrite)]
    [InlineData("serviceaccounts.read", PermissionType.ServiceAccountsRead)]
    [InlineData("serviceaccounts.write", PermissionType.ServiceAccountsWrite)]
    [InlineData("serviceaccounts.manage", PermissionType.ServiceAccountsManage)]
    [InlineData("webhooks.manage", PermissionType.WebhooksManage)]
    public void MapScopeToPermission_KnownScope_ReturnsExpectedPermission(string scope, string expectedPermission)
    {
        string? result = ScopePermissionMapper.MapScopeToPermission(scope);

        result.Should().Be(expectedPermission);
    }

    [Theory]
    [InlineData("unknown.scope")]
    [InlineData("bogus.permission")]
    [InlineData("")]
    [InlineData("not.a.real.scope")]
    public void MapScopeToPermission_UnknownScope_ReturnsNull(string scope)
    {
        string? result = ScopePermissionMapper.MapScopeToPermission(scope);

        result.Should().BeNull();
    }
}
