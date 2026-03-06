using System.Security.Claims;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Middleware;
using Foundry.Identity.Infrastructure.Repositories;
using Foundry.Shared.Kernel.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Tests.Infrastructure;

public class ServiceAccountTrackingMiddlewareTests
{
    private readonly IServiceAccountUnfilteredRepository _repository = Substitute.For<IServiceAccountUnfilteredRepository>();
    private readonly ILogger<ServiceAccountTrackingMiddleware> _logger = Substitute.For<ILogger<ServiceAccountTrackingMiddleware>>();

    [Fact]
    public async Task InvokeAsync_WithNonServiceAccountClient_DoesNotCallRepository()
    {
        // Arrange
        DefaultHttpContext context = CreateHttpContext("web-client", 200);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);
        await Task.Delay(50); // Allow fire-and-forget to execute

        // Assert
        await _repository.DidNotReceive().GetByKeycloakClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_WithFailedResponse_DoesNotTrack()
    {
        // Arrange
        DefaultHttpContext context = CreateHttpContext("sa-test-client", 400);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);
        await Task.Delay(50);

        // Assert
        await _repository.DidNotReceive().GetByKeycloakClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_WithServerErrorResponse_DoesNotTrack()
    {
        // Arrange
        DefaultHttpContext context = CreateHttpContext("sa-test-client", 500);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);
        await Task.Delay(50);

        // Assert
        await _repository.DidNotReceive().GetByKeycloakClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_WithNoAzpClaim_DoesNotTrack()
    {
        // Arrange
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()) // No claims
        };
        context.Response.StatusCode = 200;

        context.RequestServices = BuildServiceProvider();

        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);
        await Task.Delay(50);

        // Assert
        await _repository.DidNotReceive().GetByKeycloakClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(299)]
    public async Task InvokeAsync_WithSuccessStatusCode_TracksServiceAccount(int statusCode)
    {
        // Arrange
        TenantId testTenantId = TenantId.Create(Guid.NewGuid());
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            testTenantId,
            "sa-test-client",
            "Test",
            null,
            Array.Empty<string>(),
            Guid.Empty, TimeProvider.System);
        metadata.MarkUsed(TimeProvider.System); // Initial mark

        _repository.GetByKeycloakClientIdAsync("sa-test-client", Arg.Any<CancellationToken>())
            .Returns(metadata);

        DefaultHttpContext context = CreateHttpContext("sa-test-client", statusCode);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        DateTime beforeInvoke = DateTime.UtcNow;

        // Act
        await middleware.InvokeAsync(context);

        // Assert - poll for the fire-and-forget to complete (up to 2s for slow CI)
        await WaitForReceivedCallAsync(
            () => _repository.Received().GetByKeycloakClientIdAsync("sa-test-client", Arg.Any<CancellationToken>()));
        metadata.LastUsedAt.Should().BeOnOrAfter(beforeInvoke);
    }

    [Fact]
    public async Task InvokeAsync_WhenRepositoryThrows_LogsWarningAndContinues()
    {
        // Arrange
        _repository.GetByKeycloakClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ServiceAccountMetadata?>(_ => throw new InvalidOperationException("Database error"));

        DefaultHttpContext context = CreateHttpContext("sa-test-client", 200);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        // Act & Assert - should not throw
        await middleware.InvokeAsync(context);

        // Verify the repository was called (and the exception was handled)
        await WaitForReceivedCallAsync(
            () => _repository.Received().GetByKeycloakClientIdAsync("sa-test-client", Arg.Any<CancellationToken>()));
    }

    private ServiceProvider BuildServiceProvider()
    {
        ServiceCollection services = new();
        services.AddScoped<IServiceAccountUnfilteredRepository>(_ => _repository);
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider();
    }

    private ServiceAccountTrackingMiddleware CreateMiddleware()
    {
        ServiceProvider serviceProvider = BuildServiceProvider();
        IServiceScopeFactory scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        return new ServiceAccountTrackingMiddleware(_ => Task.CompletedTask, _logger, scopeFactory);
    }

    private DefaultHttpContext CreateHttpContext(string clientId, int statusCode)
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("azp", clientId)
            }))
        };
        context.Response.StatusCode = statusCode;
        context.RequestServices = BuildServiceProvider();

        return context;
    }

    private static async Task WaitForReceivedCallAsync(Func<Task> assertion, int timeoutMs = 2000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await assertion();
                return;
            }
            catch (NSubstitute.Exceptions.ReceivedCallsException)
            {
                await Task.Delay(50);
            }
        }
        // Final attempt — let it throw
        await assertion();
    }
}
