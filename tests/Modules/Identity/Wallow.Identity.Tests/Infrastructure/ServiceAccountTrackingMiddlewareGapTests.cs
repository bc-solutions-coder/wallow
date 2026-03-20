using System.Security.Claims;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Middleware;
using Wallow.Identity.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wallow.Identity.Tests.Infrastructure;

public class ServiceAccountTrackingMiddlewareGapTests
{
    private readonly IServiceAccountUnfilteredRepository _repository = Substitute.For<IServiceAccountUnfilteredRepository>();
    private readonly ILogger<ServiceAccountTrackingMiddleware> _logger = Substitute.For<ILogger<ServiceAccountTrackingMiddleware>>();

    [Fact]
    public async Task InvokeAsync_ServiceAccountNotFoundInRepository_DoesNotThrow()
    {
        _repository.GetByKeycloakClientIdAsync("sa-unknown", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ServiceAccountMetadata?>(null));

        DefaultHttpContext context = CreateHttpContext("sa-unknown", 200);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        await WaitForReceivedCallAsync(
            () => _repository.Received().GetByKeycloakClientIdAsync("sa-unknown", Arg.Any<CancellationToken>()));
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_CallsNextBeforeTracking()
    {
        bool nextCalled = false;
        int statusCodeAtNextCall = 0;

        ServiceProvider serviceProvider = BuildServiceProvider();
        IServiceScopeFactory scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        ServiceAccountTrackingMiddleware middleware = new(
            ctx =>
            {
                nextCalled = true;
                statusCodeAtNextCall = ctx.Response.StatusCode;
                return Task.CompletedTask;
            },
            _logger,
            scopeFactory);

        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("azp", "sa-test")
            ]))
        };
        context.Response.StatusCode = 200;
        context.RequestServices = serviceProvider;

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_DoesNotTrack()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Response.StatusCode = 200;
        context.RequestServices = BuildServiceProvider();

        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);
        await Task.Delay(50);

        await _repository.DidNotReceive().GetByKeycloakClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_NonServiceAccountPrefix_DoesNotTrack()
    {
        DefaultHttpContext context = CreateHttpContext("regular-client", 200);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);
        await Task.Delay(50);

        await _repository.DidNotReceive().GetByKeycloakClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_StatusCode300_DoesNotTrack()
    {
        DefaultHttpContext context = CreateHttpContext("sa-test-client", 300);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);
        await Task.Delay(50);

        await _repository.DidNotReceive().GetByKeycloakClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_StatusCode199_DoesNotTrack()
    {
        DefaultHttpContext context = CreateHttpContext("sa-test-client", 199);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);
        await Task.Delay(50);

        await _repository.DidNotReceive().GetByKeycloakClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private ServiceProvider BuildServiceProvider()
    {
        ServiceCollection services = new ServiceCollection();
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
        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("azp", clientId)
            ]))
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
        await assertion();
    }
}
