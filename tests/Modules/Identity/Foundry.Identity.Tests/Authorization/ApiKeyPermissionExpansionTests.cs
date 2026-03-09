using System.Security.Claims;
using Foundry.Identity.Application.Constants;
using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Http;

namespace Foundry.Identity.Tests.Authorization;

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
        DefaultHttpContext context = new()
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
        DefaultHttpContext context = new()
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
        DefaultHttpContext context = new()
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
    public void ValidScopes_ContainsExactlyElevenEntries()
    {
        ApiScopes.ValidScopes.Should().HaveCount(11);
    }

    [Theory]
    [InlineData("invoices.read")]
    [InlineData("invoices.write")]
    [InlineData("payments.read")]
    [InlineData("payments.write")]
    [InlineData("subscriptions.read")]
    [InlineData("subscriptions.write")]
    [InlineData("users.read")]
    [InlineData("users.write")]
    [InlineData("notifications.read")]
    [InlineData("notifications.write")]
    [InlineData("webhooks.manage")]
    public void ValidScopes_ContainsExpectedScope(string scope)
    {
        ApiScopes.ValidScopes.Should().Contain(scope);
    }
}
