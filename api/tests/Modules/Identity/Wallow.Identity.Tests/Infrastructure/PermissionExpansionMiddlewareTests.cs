using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Tests.Infrastructure;

public class PermissionExpansionMiddlewareTests
{
    /// <summary>
    /// Organization claim used by these tenant-scoped expansion fixtures.
    /// </summary>
    private const string TenantId = "0f3a1c2e-5b8d-4a71-9c62-7e4d0a1b3f56";

    [Fact]
    public async Task InvokeAsync_WithUnauthenticatedUser_DoesNotAddPermissions()
    {

        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal()
        };
        bool nextCalled = false;

        PermissionExpansionMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });


        await middleware.InvokeAsync(context);


        nextCalled.Should().BeTrue();
        context.User.Claims.Should().NotContain(c => c.Type == "permission");
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_MapsOAuth2ScopesToPermissions()
    {

        Claim[] claims = new[]
        {
            new Claim("org_id", TenantId),
            new Claim("azp", "sa-tenant123-test"),
            new Claim("scope", "storage.read storage.write")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);


        await middleware.InvokeAsync(context);


        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.StorageRead);
        permissions.Should().Contain(PermissionType.StorageWrite);
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_HandlesMultipleScopeClaims()
    {

        Claim[] claims = new[]
        {
            new Claim("org_id", TenantId),
            new Claim("azp", "sa-test"),
            new Claim("scope", "storage.read"),
            new Claim("scope", "inquiries.write")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);


        await middleware.InvokeAsync(context);


        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.StorageRead);
        permissions.Should().Contain(PermissionType.InquiriesWrite);
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_IgnoresUnknownScopes()
    {

        Claim[] claims = new[]
        {
            new Claim("org_id", TenantId),
            new Claim("azp", "sa-test"),
            new Claim("scope", "unknown.scope storage.read invalid.scope")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);


        await middleware.InvokeAsync(context);


        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().ContainSingle();
        permissions.Should().Contain(PermissionType.StorageRead);
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_MapsAllCommunicationScopes()
    {

        Claim[] claims = new[]
        {
            new Claim("org_id", TenantId),
            new Claim("azp", "sa-test"),
            new Claim("scope", "announcements.read announcements.manage notifications.read notifications.write")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);


        await middleware.InvokeAsync(context);


        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.AnnouncementRead);
        permissions.Should().Contain(PermissionType.AnnouncementManage);
        permissions.Should().Contain(PermissionType.NotificationRead);
        permissions.Should().Contain(PermissionType.NotificationsWrite);
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_MapsIdentityScopes()
    {

        Claim[] claims = new[]
        {
            new Claim("org_id", TenantId),
            new Claim("azp", "sa-test"),
            new Claim("scope", "users.read users.write")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);


        await middleware.InvokeAsync(context);


        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.UsersRead);
        permissions.Should().Contain(PermissionType.UsersUpdate); // users.write maps to UsersUpdate
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_MapsNotificationScopes()
    {

        Claim[] claims = new[]
        {
            new Claim("org_id", TenantId),
            new Claim("azp", "sa-test"),
            new Claim("scope", "notifications.read notifications.write")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);


        await middleware.InvokeAsync(context);


        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.NotificationRead);
        permissions.Should().Contain(PermissionType.NotificationsWrite);
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_MapsWebhooksScope()
    {

        Claim[] claims = new[]
        {
            new Claim("org_id", TenantId),
            new Claim("azp", "sa-test"),
            new Claim("scope", "webhooks.manage")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);


        await middleware.InvokeAsync(context);


        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.WebhooksManage);
    }

    [Fact]
    public async Task InvokeAsync_WithNonServiceAccountClient_ExpandsUserRoles()
    {

        Claim[] claims = new[]
        {
            new Claim("org_id", TenantId),
            new Claim("azp", "web-client"),
            new Claim(ClaimTypes.Role, "admin")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);


        await middleware.InvokeAsync(context);


        List<Claim> permissions = context.User.FindAll("permission").ToList();
        permissions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WithNoAzpClaim_TreatsAsRegularUser()
    {

        Claim[] claims = new[]
        {
            new Claim("org_id", TenantId),
            new Claim(ClaimTypes.Role, "user")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);


        await middleware.InvokeAsync(context);


        List<Claim> permissions = context.User.FindAll("permission").ToList();
        permissions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
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
}
