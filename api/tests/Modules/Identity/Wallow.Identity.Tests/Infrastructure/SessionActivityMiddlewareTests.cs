using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Middleware;

namespace Wallow.Identity.Tests.Infrastructure;

public class SessionActivityMiddlewareTests
{
    private readonly IConnectionMultiplexer _mux = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _redis = Substitute.For<IDatabase>();
    private readonly ISessionService _sessionService = Substitute.For<ISessionService>();

    public SessionActivityMiddlewareTests()
    {
        _mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redis);
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedRequest_SkipsDbUpdate()
    {
        bool nextCalled = false;
        SessionActivityMiddleware middleware = CreateMiddleware(() => nextCalled = true);

        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        await middleware.InvokeAsync(context, _sessionService);

        nextCalled.Should().BeTrue();
        await _sessionService.DidNotReceive()
            .TouchSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_NoCookie_SkipsDbUpdate()
    {
        bool nextCalled = false;
        SessionActivityMiddleware middleware = CreateMiddleware(() => nextCalled = true);

        DefaultHttpContext context = CreateAuthenticatedContext(sessionToken: null);

        await middleware.InvokeAsync(context, _sessionService);

        nextCalled.Should().BeTrue();
        await _sessionService.DidNotReceive()
            .TouchSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ThrottleKeyAbsent_CallsTouchSession()
    {
        // Redis StringSetAsync returns true when key was newly set (key didn't exist)
        _redis.StringSetAsync(
                Arg.Is<RedisKey>(k => k.ToString().StartsWith("session:touched:")),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(),
                Arg.Is(When.NotExists),
                Arg.Any<CommandFlags>())
            .Returns(true);

        bool nextCalled = false;
        SessionActivityMiddleware middleware = CreateMiddleware(() => nextCalled = true);

        string token = "test-session-token";
        DefaultHttpContext context = CreateAuthenticatedContext(token);

        await middleware.InvokeAsync(context, _sessionService);

        nextCalled.Should().BeTrue();
        await _sessionService.Received(1)
            .TouchSessionAsync(token, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ThrottleKeyPresent_SkipsDbUpdate()
    {
        // Redis StringSetAsync returns false when key already existed
        _redis.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(),
                Arg.Is(When.NotExists),
                Arg.Any<CommandFlags>())
            .Returns(false);

        bool nextCalled = false;
        SessionActivityMiddleware middleware = CreateMiddleware(() => nextCalled = true);

        DefaultHttpContext context = CreateAuthenticatedContext("test-session-token");

        await middleware.InvokeAsync(context, _sessionService);

        nextCalled.Should().BeTrue();
        await _sessionService.DidNotReceive()
            .TouchSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext()
    {
        _redis.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(),
                Arg.Any<When>(),
                Arg.Any<CommandFlags>())
            .Returns(true);

        bool nextCalled = false;
        SessionActivityMiddleware middleware = CreateMiddleware(() => nextCalled = true);

        DefaultHttpContext context = CreateAuthenticatedContext("some-token");

        await middleware.InvokeAsync(context, _sessionService);

        nextCalled.Should().BeTrue();
    }

    private SessionActivityMiddleware CreateMiddleware(Action? onNext = null)
    {
        return new SessionActivityMiddleware(
            _ =>
            {
                onNext?.Invoke();
                return Task.CompletedTask;
            },
            _mux);
    }

    private static DefaultHttpContext CreateAuthenticatedContext(string? sessionToken)
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
            ], "Bearer"))
        };

        if (sessionToken is not null)
        {
            context.Request.Headers.Append("Cookie", $"wallow.session={sessionToken}");
        }

        return context;
    }
}
