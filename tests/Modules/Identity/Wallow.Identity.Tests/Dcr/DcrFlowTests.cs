using System.Security.Claims;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Identity.Infrastructure.Middleware;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Wallow.Identity.Tests.Dcr;

/// <summary>
/// Verifies core DCR (Dynamic Client Registration) flow behaviors:
/// - sa-prefixed clients get scope-based permission expansion
/// - Non-sa-prefixed clients get role-based expansion (no roles = no permissions)
/// - ServiceAccountTrackingMiddleware lazily creates metadata for unknown sa-* clients
/// </summary>
public class DcrFlowTests
{
    [Fact]
    public async Task ServiceAccount_WithSaPrefix_GetsPermissionsFromScopes()
    {
        // A DCR-registered client with sa- prefix and inquiries scopes should get
        // inquiries.read and inquiries.write mapped to permission claims
        List<Claim> claims =
        [
            new Claim("azp", "sa-wallow-api"),
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
        // Simulates a token from a DCR-registered sa-wallow-api client
        // that has both inquiries scopes and the wallow-api audience
        List<Claim> claims =
        [
            new Claim("azp", "sa-wallow-api"),
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

        // Verify audience claim is present
        string? audience = httpContext.User.FindFirst("aud")?.Value;
        audience.Should().Be("wallow-api");

        // Verify both scope permissions are expanded
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
        // A user client without the sa- prefix gets role-based expansion (empty here)
        // plus scope-based expansion as a supplement
        List<Claim> claims =
        [
            new Claim("azp", "my-frontend-app"),
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
        // When an sa-* client makes a successful API call,
        // the tracking middleware should record the client ID to the buffer
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
        // Clients without sa- or app- prefix should not be recorded
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
        // app-* prefixed clients should also be recorded
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
