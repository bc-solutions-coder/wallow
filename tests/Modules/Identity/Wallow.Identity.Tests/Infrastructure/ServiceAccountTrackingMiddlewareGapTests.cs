using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Infrastructure.Middleware;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public class ServiceAccountTrackingMiddlewareGapTests
{
    private readonly ILogger<ServiceAccountTrackingMiddleware> _logger = Substitute.For<ILogger<ServiceAccountTrackingMiddleware>>();
    private readonly ServiceAccountUsageBuffer _buffer = new();

    [Fact]
    public async Task InvokeAsync_CallsNextBeforeRecording()
    {
        bool nextCalled = false;

        ServiceAccountTrackingMiddleware middleware = new(
            ctx =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            _logger,
            _buffer);

        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("azp", "sa-test")
            ]))
        };
        context.Response.StatusCode = 200;

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_DoesNotRecord()
    {
        DefaultHttpContext context = new();
        context.Response.StatusCode = 200;

        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Dictionary<string, DateTimeOffset> entries = _buffer.DrainAll();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_NonServiceAccountPrefix_DoesNotRecord()
    {
        DefaultHttpContext context = CreateHttpContext("regular-client", 200);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Dictionary<string, DateTimeOffset> entries = _buffer.DrainAll();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_StatusCode300_DoesNotRecord()
    {
        DefaultHttpContext context = CreateHttpContext("sa-test-client", 300);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Dictionary<string, DateTimeOffset> entries = _buffer.DrainAll();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_StatusCode199_DoesNotRecord()
    {
        DefaultHttpContext context = CreateHttpContext("sa-test-client", 199);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Dictionary<string, DateTimeOffset> entries = _buffer.DrainAll();
        entries.Should().BeEmpty();
    }

    private ServiceAccountTrackingMiddleware CreateMiddleware()
    {
        return new ServiceAccountTrackingMiddleware(_ => Task.CompletedTask, _logger, _buffer);
    }

    private static DefaultHttpContext CreateHttpContext(string clientId, int statusCode)
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("azp", clientId)
            ]))
        };
        context.Response.StatusCode = statusCode;
        return context;
    }
}
