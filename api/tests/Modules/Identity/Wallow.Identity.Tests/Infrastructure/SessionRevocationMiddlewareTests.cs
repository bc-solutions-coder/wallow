using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using Wallow.Identity.Infrastructure.Middleware;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class SessionRevocationMiddlewareTests
{
#pragma warning disable CA2213 // IConnectionMultiplexer is a mock
    private readonly IConnectionMultiplexer _mux;
#pragma warning restore CA2213
    private readonly IDatabase _redis;

    public SessionRevocationMiddlewareTests()
    {
        _mux = Substitute.For<IConnectionMultiplexer>();
        _redis = Substitute.For<IDatabase>();
        _mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redis);
    }

    [Fact]
    public async Task InvokeAsync_NoSessionCookie_CallsNext()
    {
        // Arrange
        bool nextCalled = false;
        SessionRevocationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _mux);

        DefaultHttpContext context = new();
        context.User = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_SessionNotRevoked_CallsNext()
    {
        // Arrange
        string token = "valid-session-token";
        _redis.KeyExistsAsync($"session:revoked:{token}", Arg.Any<CommandFlags>())
            .Returns(false);

        bool nextCalled = false;
        SessionRevocationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _mux);

        DefaultHttpContext context = new();
        context.User = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));
        context.Request.Headers.Append("Cookie", $"wallow.session={token}");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_SessionRevoked_Returns401AndClearsCookie()
    {
        // Arrange
        string token = "revoked-session-token";
        _redis.KeyExistsAsync($"session:revoked:{token}", Arg.Any<CommandFlags>())
            .Returns(true);

        SessionRevocationMiddleware middleware = new(_ => Task.CompletedTask, _mux);

        DefaultHttpContext context = new();
        context.User = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));
        context.Request.Headers.Append("Cookie", $"wallow.session={token}");
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
        context.Response.Headers.SetCookie.ToString().Should().Contain("wallow.session=");
        context.Response.ContentType.Should().Be("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using JsonDocument doc = await JsonDocument.ParseAsync(context.Response.Body);
        doc.RootElement.GetProperty("error").GetString().Should().Be("session_revoked");
    }

    [Fact]
    public async Task InvokeAsync_SessionRevoked_DoesNotCallNext()
    {
        // Arrange
        string token = "revoked-session-token";
        _redis.KeyExistsAsync($"session:revoked:{token}", Arg.Any<CommandFlags>())
            .Returns(true);

        bool nextCalled = false;
        SessionRevocationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _mux);

        DefaultHttpContext context = new();
        context.User = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));
        context.Request.Headers.Append("Cookie", $"wallow.session={token}");
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedWithCookie_CallsNext()
    {
        // Arrange
        bool nextCalled = false;
        SessionRevocationMiddleware middleware = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _mux);

        DefaultHttpContext context = new();
        context.User = new ClaimsPrincipal(); // Not authenticated
        context.Request.Headers.Append("Cookie", "wallow.session=some-token");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }
}
