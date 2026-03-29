using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Infrastructure.Middleware;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public class ServiceAccountTrackingMiddlewareTests
{
    private readonly ILogger<ServiceAccountTrackingMiddleware> _logger = Substitute.For<ILogger<ServiceAccountTrackingMiddleware>>();
    private readonly ServiceAccountUsageBuffer _buffer = new();

    [Fact]
    public async Task InvokeAsync_WithNonServiceAccountClient_DoesNotRecord()
    {
        DefaultHttpContext context = CreateHttpContext("web-client", 200);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Dictionary<string, DateTimeOffset> entries = _buffer.DrainAll();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WithFailedResponse_DoesNotRecord()
    {
        DefaultHttpContext context = CreateHttpContext("sa-test-client", 400);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Dictionary<string, DateTimeOffset> entries = _buffer.DrainAll();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WithServerErrorResponse_DoesNotRecord()
    {
        DefaultHttpContext context = CreateHttpContext("sa-test-client", 500);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Dictionary<string, DateTimeOffset> entries = _buffer.DrainAll();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WithNoAzpClaim_DoesNotRecord()
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        context.Response.StatusCode = 200;

        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Dictionary<string, DateTimeOffset> entries = _buffer.DrainAll();
        entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(299)]
    public async Task InvokeAsync_WithSuccessStatusCode_RecordsServiceAccount(int statusCode)
    {
        DefaultHttpContext context = CreateHttpContext("sa-test-client", statusCode);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        DateTimeOffset beforeInvoke = DateTimeOffset.UtcNow;

        await middleware.InvokeAsync(context);

        Dictionary<string, DateTimeOffset> entries = _buffer.DrainAll();
        entries.Should().ContainKey("sa-test-client");
        entries["sa-test-client"].Should().BeOnOrAfter(beforeInvoke);
    }

    [Fact]
    public async Task InvokeAsync_WithAppPrefix_RecordsUsage()
    {
        DefaultHttpContext context = CreateHttpContext("app-test-client", 200);
        ServiceAccountTrackingMiddleware middleware = CreateMiddleware();

        await middleware.InvokeAsync(context);

        Dictionary<string, DateTimeOffset> entries = _buffer.DrainAll();
        entries.Should().ContainKey("app-test-client");
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
