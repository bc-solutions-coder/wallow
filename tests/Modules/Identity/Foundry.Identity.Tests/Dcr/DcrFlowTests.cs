using System.Security.Claims;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Authorization;
using Foundry.Identity.Infrastructure.Middleware;
using Foundry.Identity.Infrastructure.Repositories;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foundry.Identity.Tests.Dcr;

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
            new Claim("azp", "sa-foundry-api"),
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
        // Simulates a token from a DCR-registered sa-foundry-api client
        // that has both inquiries scopes and the foundry-api audience
        List<Claim> claims =
        [
            new Claim("azp", "sa-foundry-api"),
            new Claim("aud", "foundry-api"),
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
        audience.Should().Be("foundry-api");

        // Verify both scope permissions are expanded
        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().HaveCount(2);
        permissions.Should().Contain(PermissionType.InquiriesRead);
        permissions.Should().Contain(PermissionType.InquiriesWrite);
    }

    [Fact]
    public async Task Client_WithoutSaPrefix_GetsRoleBasedExpansion_NoRolesMeansNoPermissions()
    {
        // A client without the sa- prefix (e.g., a frontend app) should go through
        // role-based expansion. With no roles, it gets zero permissions -> 403 in practice
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

        // Without sa- prefix and without roles, no permissions should be added
        List<string> permissions = httpContext.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();
        permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task TrackingMiddleware_UnknownSaClient_CreatesMetadataRecord()
    {
        // When an unknown sa-* client makes a successful API call,
        // the tracking middleware should lazily create a ServiceAccountMetadata record
        IServiceAccountUnfilteredRepository unfilteredRepo = Substitute.For<IServiceAccountUnfilteredRepository>();
        unfilteredRepo.GetByKeycloakClientIdAsync("sa-new-client", Arg.Any<CancellationToken>())
            .Returns((ServiceAccountMetadata?)null);

        IServiceAccountRepository repository = Substitute.For<IServiceAccountRepository>();
        TimeProvider timeProvider = TimeProvider.System;

        ServiceCollection services = new();
        services.AddSingleton(unfilteredRepo);
        services.AddSingleton(repository);
        services.AddSingleton(timeProvider);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
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
            scopeFactory);

        await middleware.InvokeAsync(httpContext);

        // Give fire-and-forget task time to complete
        await Task.Delay(500);

        repository.Received(1).Add(Arg.Is<ServiceAccountMetadata>(m =>
            m.KeycloakClientId == "sa-new-client"));
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackingMiddleware_NonSaClient_DoesNotTrack()
    {
        // Clients without sa- prefix should not be tracked by the middleware
        IServiceAccountUnfilteredRepository unfilteredRepo = Substitute.For<IServiceAccountUnfilteredRepository>();
        IServiceAccountRepository repository = Substitute.For<IServiceAccountRepository>();
        TimeProvider timeProvider = TimeProvider.System;

        ServiceCollection services = new();
        services.AddSingleton(unfilteredRepo);
        services.AddSingleton(repository);
        services.AddSingleton(timeProvider);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
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
            scopeFactory);

        await middleware.InvokeAsync(httpContext);

        await Task.Delay(200);

        await unfilteredRepo.DidNotReceive().GetByKeycloakClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        repository.DidNotReceive().Add(Arg.Any<ServiceAccountMetadata>());
    }

    [Fact]
    public async Task TrackingMiddleware_KnownSaClient_UpdatesLastUsed()
    {
        // When a known sa-* client makes a request, the middleware should
        // update the existing metadata's LastUsedAt timestamp
        ServiceAccountMetadata existingMetadata = ServiceAccountMetadata.Create(
            TenantId.Platform,
            "sa-existing-client",
            "Existing Client",
            null,
            [],
            Guid.Empty,
            TimeProvider.System);

        IServiceAccountUnfilteredRepository unfilteredRepo = Substitute.For<IServiceAccountUnfilteredRepository>();
        unfilteredRepo.GetByKeycloakClientIdAsync("sa-existing-client", Arg.Any<CancellationToken>())
            .Returns(existingMetadata);

        IServiceAccountRepository repository = Substitute.For<IServiceAccountRepository>();
        TimeProvider timeProvider = TimeProvider.System;

        ServiceCollection services = new();
        services.AddSingleton(unfilteredRepo);
        services.AddSingleton(repository);
        services.AddSingleton(timeProvider);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        ILogger<ServiceAccountTrackingMiddleware> logger = NullLogger<ServiceAccountTrackingMiddleware>.Instance;

        List<Claim> claims = [new Claim("azp", "sa-existing-client")];
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
            scopeFactory);

        await middleware.InvokeAsync(httpContext);

        await Task.Delay(500);

        existingMetadata.LastUsedAt.Should().NotBeNull();
        await unfilteredRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        // Should NOT create a new record since it already exists
        repository.DidNotReceive().Add(Arg.Any<ServiceAccountMetadata>());
    }
}
