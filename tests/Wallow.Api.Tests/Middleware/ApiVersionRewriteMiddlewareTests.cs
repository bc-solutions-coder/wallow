using Microsoft.AspNetCore.Http;
using Wallow.Api.Middleware;

namespace Wallow.Api.Tests.Middleware;

public class ApiVersionRewriteMiddlewareTests
{
    private bool _nextCalled;
    private readonly RequestDelegate _next;

    public ApiVersionRewriteMiddlewareTests()
    {
        _next = _ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        };
    }

    [Theory]
    [InlineData("/api/users", "/api/v1/users")]
    [InlineData("/api/billing/invoices", "/api/v1/billing/invoices")]
    [InlineData("/api/health", "/api/v1/health")]
    public async Task InvokeAsync_NonVersionedApiPath_RewritesToV1(string original, string expected)
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = original;

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().Be(expected);
        _nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v2/billing/invoices")]
    [InlineData("/api/V1/health")]
    [InlineData("/api/v3/resource")]
    public async Task InvokeAsync_VersionedApiPath_DoesNotRewrite(string path)
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = path;

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().Be(path);
        _nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/")]
    [InlineData("/swagger")]
    [InlineData("/hangfire")]
    public async Task InvokeAsync_NonApiPath_PassesThroughUnchanged(string path)
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = path;

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().Be(path);
        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_NullPath_PassesThroughUnchanged()
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = new PathString(null);

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().BeNullOrEmpty();
        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_CaseInsensitiveApiPrefix_RewritesPath()
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = "/API/users";

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().Be("/api/v1/users");
        _nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/value", "/api/v1/value")]
    [InlineData("/api/versions", "/api/v1/versions")]
    public async Task InvokeAsync_PathStartingWithVButNoDigit_RewritesToV1(string original, string expected)
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = original;

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().Be(expected);
        _nextCalled.Should().BeTrue();
    }
}
