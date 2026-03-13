using System.Security.Claims;
using System.Text.Json;
using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Http;

namespace Foundry.Identity.Tests.Infrastructure;

public class PermissionExpansionMiddlewareGapTests
{
    private static readonly string[] _userRoles = ["user"];
    private static readonly string[] _readPermissions = ["read"];
    [Fact]
    public async Task InvokeAsync_ApiKeyAuthMethod_ExpandsScopesToPermissions()
    {
        Claim[] claims =
        [
            new("auth_method", "api_key"),
            new("scope", "invoices.read users.read")
        ];

        ClaimsIdentity identity = new(claims, "ApiKey");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.InvoicesRead);
        permissions.Should().Contain(PermissionType.UsersRead);
    }

    [Fact]
    public async Task InvokeAsync_UserWithKeycloakRealmAccessFallback_ExpandsPermissions()
    {
        string realmAccess = JsonSerializer.Serialize(new { roles = _userRoles });
        Claim[] claims =
        [
            new("azp", "web-client"),
            new("realm_access", realmAccess)
        ];

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        List<Claim> permissions = context.User.FindAll("permission").ToList();
        permissions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_UserWithInvalidRealmAccessJson_DoesNotThrow()
    {
        Claim[] claims =
        [
            new("azp", "web-client"),
            new("realm_access", "not-valid-json")
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
    public async Task InvokeAsync_UserWithNoRolesAndNoRealmAccess_AddsNoPermissions()
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
    public async Task InvokeAsync_RealmAccessWithNoRolesProperty_AddsNoPermissions()
    {
        string realmAccess = JsonSerializer.Serialize(new { permissions = _readPermissions });
        Claim[] claims =
        [
            new("azp", "web-client"),
            new("realm_access", realmAccess)
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
    [InlineData("billing.read", PermissionType.BillingRead)]
    [InlineData("billing.manage", PermissionType.BillingManage)]
    [InlineData("payments.read", PermissionType.PaymentsRead)]
    [InlineData("payments.write", PermissionType.PaymentsWrite)]
    [InlineData("subscriptions.read", PermissionType.SubscriptionsRead)]
    [InlineData("subscriptions.write", PermissionType.SubscriptionsWrite)]
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
