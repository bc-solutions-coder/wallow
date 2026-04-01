using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Tests.Infrastructure;

public class PermissionExpansionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithUnauthenticatedUser_DoesNotAddPermissions()
    {
        // Arrange
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal() // Not authenticated
        };
        bool nextCalled = false;

        PermissionExpansionMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.User.Claims.Should().NotContain(c => c.Type == "permission");
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_MapsOAuth2ScopesToPermissions()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim("azp", "sa-tenant123-test"),
            new Claim("scope", "storage.read storage.write")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.StorageRead);
        permissions.Should().Contain(PermissionType.StorageWrite);
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_HandlesMultipleScopeClaims()
    {
        // Arrange
        Claim[] claims = new[]
        {
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

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.StorageRead);
        permissions.Should().Contain(PermissionType.InquiriesWrite);
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_IgnoresUnknownScopes()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim("azp", "sa-test"),
            new Claim("scope", "unknown.scope storage.read invalid.scope")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().ContainSingle();
        permissions.Should().Contain(PermissionType.StorageRead);
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_MapsAllCommunicationScopes()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim("azp", "sa-test"),
            new Claim("scope", "messaging.access announcements.read announcements.manage changelog.manage notifications.read notifications.write")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.MessagingAccess);
        permissions.Should().Contain(PermissionType.AnnouncementRead);
        permissions.Should().Contain(PermissionType.AnnouncementManage);
        permissions.Should().Contain(PermissionType.ChangelogManage);
        permissions.Should().Contain(PermissionType.NotificationRead);
        permissions.Should().Contain(PermissionType.NotificationsWrite);
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_MapsIdentityScopes()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim("azp", "sa-test"),
            new Claim("scope", "users.read users.write")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.UsersRead);
        permissions.Should().Contain(PermissionType.UsersUpdate); // users.write maps to UsersUpdate
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_MapsNotificationScopes()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim("azp", "sa-test"),
            new Claim("scope", "notifications.read notifications.write")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.NotificationRead);
        permissions.Should().Contain(PermissionType.NotificationsWrite);
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_MapsWebhooksScope()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim("azp", "sa-test"),
            new Claim("scope", "webhooks.manage")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.WebhooksManage);
    }

    [Fact]
    public async Task InvokeAsync_WithNonServiceAccountClient_ExpandsUserRoles()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim("azp", "web-client"), // Not a service account
            new Claim(ClaimTypes.Role, "admin")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<Claim> permissions = context.User.FindAll("permission").ToList();
        permissions.Should().NotBeEmpty(); // Admin role should expand to many permissions
    }

    [Fact]
    public async Task InvokeAsync_WithNoAzpClaim_TreatsAsRegularUser()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim(ClaimTypes.Role, "user")
        };

        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<Claim> permissions = context.User.FindAll("permission").ToList();
        permissions.Should().NotBeEmpty(); // User role should expand to permissions
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        // Arrange
        DefaultHttpContext context = new DefaultHttpContext();
        bool nextCalled = false;

        PermissionExpansionMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }
}
