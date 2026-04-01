using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Tests.Infrastructure;

public class PermissionExpansionMiddlewareGapTests
{
    [Fact]
    public async Task InvokeAsync_ApiKeyAuthMethod_ExpandsScopesToPermissions()
    {
        Claim[] claims =
        [
            new("auth_method", "api_key"),
            new("scope", "storage.read users.read")
        ];

        ClaimsIdentity identity = new(claims, "ApiKey");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.StorageRead);
        permissions.Should().Contain(PermissionType.UsersRead);
    }

    [Fact]
    public async Task InvokeAsync_UserWithNoRolesAndNoScopes_AddsNoPermissions()
    {
        Claim[] claims =
        [
            new("azp", "web-client"),
            new("sub", Guid.NewGuid().ToString())
        ];

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.User.FindAll("permission").Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_UserWithNoRolesButWithScopes_GetsScopePermissions()
    {
        Claim[] claims =
        [
            new("client_id", "wallow-dev-client"),
            new("sub", Guid.NewGuid().ToString()),
            new("scope", "notifications.read inquiries.write")
        ];

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.NotificationRead);
        permissions.Should().Contain(PermissionType.InquiriesWrite);
    }

    [Fact]
    public async Task InvokeAsync_ServiceAccountWithNoScopes_AddsNoPermissions()
    {
        Claim[] claims =
        [
            new("azp", "sa-test-empty")
        ];

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.User.FindAll("permission").Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ServiceAccountWithAllUnknownScopes_AddsNoPermissions()
    {
        Claim[] claims =
        [
            new("azp", "sa-test-unknown"),
            new("scope", "unknown.one unknown.two")
        ];

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.User.FindAll("permission").Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext_EvenWhenUnauthenticated()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        bool nextCalled = false;

        PermissionExpansionMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext_WhenAuthenticated()
    {
        Claim[] claims =
        [
            new("azp", "sa-test"),
            new("scope", "invoices.read")
        ];

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        bool nextCalled = false;

        PermissionExpansionMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Theory]
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
    [InlineData("notifications.read", PermissionType.NotificationRead)]
    [InlineData("notifications.write", PermissionType.NotificationsWrite)]
    [InlineData("configuration.read", PermissionType.ConfigurationRead)]
    [InlineData("configuration.manage", PermissionType.ConfigurationManage)]
    [InlineData("serviceaccounts.read", PermissionType.ServiceAccountsRead)]
    [InlineData("serviceaccounts.write", PermissionType.ServiceAccountsWrite)]
    [InlineData("serviceaccounts.manage", PermissionType.ServiceAccountsManage)]
    [InlineData("webhooks.manage", PermissionType.WebhooksManage)]
    public async Task InvokeAsync_ServiceAccountScope_MapsToExpectedPermission(string scope, string expectedPermission)
    {
        Claim[] claims =
        [
            new("azp", "sa-test-scope"),
            new("scope", scope)
        ];

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(expectedPermission);
    }
}
