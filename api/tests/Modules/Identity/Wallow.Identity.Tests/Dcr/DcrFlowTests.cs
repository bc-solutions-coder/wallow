using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Identity.Infrastructure.Middleware;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Tests.Dcr;

/// <summary>
/// Checks permission expansion for synthetic client principals and client-ID usage buffering.
/// </summary>
public class DcrFlowTests
{
    /// <summary>
    /// Organization claim required by the permission-expansion fixtures.
    /// </summary>
    private const string TenantId = "0f3a1c2e-5b8d-4a71-9c62-7e4d0a1b3f56";

    [Fact]
    public async Task ServiceAccount_WithSaPrefix_GetsPermissionsFromScopes()
    {

        List<Claim> claims =
        [
            new Claim("azp", "sa-wallow-api"),
            new Claim("org_id", TenantId),
            new Claim("scope", "inquiries.read inquiries.write")
        ];
        ClaimsIdentity identity = new(claims, "Bearer");
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new()
        {
            User = principal
        };

        bool nextCalled = false;
        PermissionExpansionMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().Contain(PermissionType.InquiriesRead);
        permissions.Should().Contain(PermissionType.InquiriesWrite);
    }

    [Fact]
    public async Task ServiceAccount_WithSaPrefix_TokenContainsBothScopesAndAudience()
    {
        // Supply scopes and audience on a synthetic service-account principal.
        List<Claim> claims =
        [
            new Claim("azp", "sa-wallow-api"),
            new Claim("org_id", TenantId),
            new Claim("aud", "wallow-api"),
            new Claim("scope", "inquiries.read inquiries.write")
        ];
        ClaimsIdentity identity = new(claims, "Bearer");
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new()
        {
            User = principal
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext);


        string? audience = httpContext.User.FindFirst("aud")?.Value;
        audience.Should().Be("wallow-api");


        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().HaveCount(2);
        permissions.Should().Contain(PermissionType.InquiriesRead);
        permissions.Should().Contain(PermissionType.InquiriesWrite);
    }

    [Fact]
    public async Task Client_WithoutSaPrefix_NoRoles_StillGetsScopePermissions()
    {
        // Scope expansion also applies to a user client without role claims.
        List<Claim> claims =
        [
            new Claim("azp", "my-frontend-app"),
            new Claim("org_id", TenantId),
            new Claim("scope", "inquiries.read inquiries.write")
        ];
        ClaimsIdentity identity = new(claims, "Bearer");
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new()
        {
            User = principal
        };

        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext);

        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().Contain(PermissionType.InquiriesRead);
        permissions.Should().Contain(PermissionType.InquiriesWrite);
    }

    [Fact]
    public async Task TrackingMiddleware_SaClient_RecordsToBuffer()
    {

        ServiceAccountUsageBuffer buffer = new();
        ILogger<ServiceAccountTrackingMiddleware> logger = NullLogger<ServiceAccountTrackingMiddleware>.Instance;

        List<Claim> claims = [new Claim("azp", "sa-new-client")];
        ClaimsIdentity identity = new(claims, "Bearer");
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new()
        {
            User = principal
        };
        httpContext.Response.StatusCode = 200;

        ServiceAccountTrackingMiddleware middleware = new(
            _ => Task.CompletedTask,
            logger,
            buffer);

        await middleware.InvokeAsync(httpContext);

        Dictionary<string, DateTimeOffset> entries = buffer.DrainAll();
        entries.Should().ContainKey("sa-new-client");
    }

    [Fact]
    public async Task TrackingMiddleware_NonSaClient_DoesNotRecord()
    {

        ServiceAccountUsageBuffer buffer = new();
        ILogger<ServiceAccountTrackingMiddleware> logger = NullLogger<ServiceAccountTrackingMiddleware>.Instance;

        List<Claim> claims = [new Claim("azp", "regular-client")];
        ClaimsIdentity identity = new(claims, "Bearer");
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new()
        {
            User = principal
        };
        httpContext.Response.StatusCode = 200;

        ServiceAccountTrackingMiddleware middleware = new(
            _ => Task.CompletedTask,
            logger,
            buffer);

        await middleware.InvokeAsync(httpContext);

        Dictionary<string, DateTimeOffset> entries = buffer.DrainAll();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task TrackingMiddleware_AppClient_RecordsToBuffer()
    {

        ServiceAccountUsageBuffer buffer = new();
        ILogger<ServiceAccountTrackingMiddleware> logger = NullLogger<ServiceAccountTrackingMiddleware>.Instance;

        List<Claim> claims = [new Claim("azp", "app-existing-client")];
        ClaimsIdentity identity = new(claims, "Bearer");
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new()
        {
            User = principal
        };
        httpContext.Response.StatusCode = 200;

        ServiceAccountTrackingMiddleware middleware = new(
            _ => Task.CompletedTask,
            logger,
            buffer);

        await middleware.InvokeAsync(httpContext);

        Dictionary<string, DateTimeOffset> entries = buffer.DrainAll();
        entries.Should().ContainKey("app-existing-client");
    }
}
