using System.Security.Claims;
using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Http;

namespace Foundry.Identity.Tests.Infrastructure;

public class PermissionExpansionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithUnauthenticatedUser_DoesNotAddPermissions()
    {
        // Arrange
        DefaultHttpContext context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(); // Not authenticated
        bool nextCalled = false;

        PermissionExpansionMiddleware middleware = new PermissionExpansionMiddleware(_ =>
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
            new Claim("scope", "invoices.read invoices.write")
        };

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new PermissionExpansionMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.InvoicesRead.ToString());
        permissions.Should().Contain(PermissionType.InvoicesWrite.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_HandlesMultipleScopeClaims()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim("azp", "sa-test"),
            new Claim("scope", "invoices.read"),
            new Claim("scope", "payments.write")
        };

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new PermissionExpansionMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.InvoicesRead.ToString());
        permissions.Should().Contain(PermissionType.PaymentsWrite.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_IgnoresUnknownScopes()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim("azp", "sa-test"),
            new Claim("scope", "unknown.scope invoices.read invalid.scope")
        };

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new PermissionExpansionMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().ContainSingle();
        permissions.Should().Contain(PermissionType.InvoicesRead.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WithServiceAccount_MapsAllBillingScopes()
    {
        // Arrange
        Claim[] claims = new[]
        {
            new Claim("azp", "sa-test"),
            new Claim("scope", "invoices.read invoices.write payments.read payments.write subscriptions.read subscriptions.write")
        };

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new PermissionExpansionMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.InvoicesRead.ToString());
        permissions.Should().Contain(PermissionType.InvoicesWrite.ToString());
        permissions.Should().Contain(PermissionType.PaymentsRead.ToString());
        permissions.Should().Contain(PermissionType.PaymentsWrite.ToString());
        permissions.Should().Contain(PermissionType.SubscriptionsRead.ToString());
        permissions.Should().Contain(PermissionType.SubscriptionsWrite.ToString());
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

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new PermissionExpansionMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.UsersRead.ToString());
        permissions.Should().Contain(PermissionType.UsersUpdate.ToString()); // users.write maps to UsersUpdate
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

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new PermissionExpansionMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.NotificationsRead.ToString());
        permissions.Should().Contain(PermissionType.NotificationsWrite.ToString());
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

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new PermissionExpansionMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        List<string> permissions = context.User.FindAll("permission").Select(c => c.Value).ToList();
        permissions.Should().Contain(PermissionType.WebhooksManage.ToString());
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

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new PermissionExpansionMiddleware(_ => Task.CompletedTask);

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

        ClaimsIdentity identity = new ClaimsIdentity(claims, "Bearer");
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new PermissionExpansionMiddleware(_ => Task.CompletedTask);

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

        PermissionExpansionMiddleware middleware = new PermissionExpansionMiddleware(_ =>
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
