using System.Security.Claims;
using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Http;

namespace Foundry.Identity.Tests.Dcr;

/// <summary>
/// Verifies that OAuth2 scopes are correctly mapped to permissions by the
/// PermissionExpansionMiddleware for service account (sa-*) clients.
/// </summary>
public class ScopeVerificationTests
{
    [Theory]
    [InlineData("inquiries.read", PermissionType.InquiriesRead)]
    [InlineData("inquiries.write", PermissionType.InquiriesWrite)]
    [InlineData("invoices.read", PermissionType.InvoicesRead)]
    [InlineData("invoices.write", PermissionType.InvoicesWrite)]
    [InlineData("payments.read", PermissionType.PaymentsRead)]
    [InlineData("payments.write", PermissionType.PaymentsWrite)]
    [InlineData("subscriptions.read", PermissionType.SubscriptionsRead)]
    [InlineData("subscriptions.write", PermissionType.SubscriptionsWrite)]
    [InlineData("users.read", PermissionType.UsersRead)]
    [InlineData("users.write", PermissionType.UsersUpdate)]
    [InlineData("notifications.read", PermissionType.NotificationsRead)]
    [InlineData("notifications.write", PermissionType.NotificationsWrite)]
    [InlineData("showcases.read", PermissionType.ShowcasesRead)]
    [InlineData("showcases.manage", PermissionType.ShowcasesManage)]
    [InlineData("storage.read", PermissionType.StorageRead)]
    [InlineData("storage.write", PermissionType.StorageWrite)]
    [InlineData("serviceaccounts.read", PermissionType.ServiceAccountsRead)]
    [InlineData("serviceaccounts.write", PermissionType.ServiceAccountsWrite)]
    [InlineData("serviceaccounts.manage", PermissionType.ServiceAccountsManage)]
    [InlineData("webhooks.manage", PermissionType.WebhooksManage)]
    public async Task ServiceAccount_Scope_MapsToCorrectPermission(string scope, string expectedPermission)
    {
        DefaultHttpContext httpContext = CreateServiceAccountContext("sa-test-client", scope);

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext);

        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().ContainSingle()
            .Which.Should().Be(expectedPermission);
    }

    [Fact]
    public async Task ServiceAccount_MultipleScopes_AllMappedToPermissions()
    {
        DefaultHttpContext httpContext = CreateServiceAccountContext(
            "sa-full-access",
            "inquiries.read inquiries.write showcases.read storage.read");

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext);

        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().HaveCount(4);
        permissions.Should().Contain(PermissionType.InquiriesRead);
        permissions.Should().Contain(PermissionType.InquiriesWrite);
        permissions.Should().Contain(PermissionType.ShowcasesRead);
        permissions.Should().Contain(PermissionType.StorageRead);
    }

    [Fact]
    public async Task ServiceAccount_UnknownScope_IsIgnored()
    {
        DefaultHttpContext httpContext = CreateServiceAccountContext(
            "sa-test-client",
            "inquiries.read unknown.scope nonexistent.permission");

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext);

        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().ContainSingle()
            .Which.Should().Be(PermissionType.InquiriesRead);
    }

    [Fact]
    public async Task ServiceAccount_NoScopes_GetsNoPermissions()
    {
        DefaultHttpContext httpContext = CreateServiceAccountContext("sa-empty-client", "");

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext);

        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task ServiceAccount_OnlyOpenIdScopes_GetsNoPermissions()
    {
        // Standard OIDC scopes like openid, profile, email should not map to any permission
        DefaultHttpContext httpContext = CreateServiceAccountContext(
            "sa-basic-client",
            "openid profile email");

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext);

        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task NonServiceAccount_WithScopes_DoesNotGetScopePermissions()
    {
        // A regular user client (no sa- prefix) should not get scope-based permissions
        // even if the token happens to contain scope claims
        List<Claim> claims =
        [
            new Claim("azp", "frontend-app"),
            new Claim("scope", "inquiries.read inquiries.write")
        ];
        ClaimsIdentity identity = new(claims, "Bearer");
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(identity)
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext);

        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task Unauthenticated_Request_GetsNoPermissions()
    {
        DefaultHttpContext httpContext = new();

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext);

        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().BeEmpty();
    }

    private static DefaultHttpContext CreateServiceAccountContext(string clientId, string scopes)
    {
        List<Claim> claims =
        [
            new Claim("azp", clientId),
            new Claim("scope", scopes)
        ];
        ClaimsIdentity identity = new(claims, "Bearer");
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
    }
}
