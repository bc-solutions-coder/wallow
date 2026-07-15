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
    [InlineData("/identity/users", "/v1/identity/users")]
    [InlineData("/storage/files", "/v1/storage/files")]
    [InlineData("/notifications/settings", "/v1/notifications/settings")]
    [InlineData("/announcements/active", "/v1/announcements/active")]
    [InlineData("/inquiries/submit", "/v1/inquiries/submit")]
    [InlineData("/branding/config", "/v1/branding/config")]
    public async Task InvokeAsync_NonVersionedPath_RewritesToV1(string original, string expected)
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = original;

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().Be(expected);
        _nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/v1/users")]
    [InlineData("/v2/billing/invoices")]
    [InlineData("/V1/notifications")]
    [InlineData("/v3/resource")]
    [InlineData("/v1/identity/users")]
    public async Task InvokeAsync_AlreadyVersionedPath_DoesNotRewrite(string path)
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = path;

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().Be(path);
        _nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/connect/token")]
    [InlineData("/connect/authorize")]
    [InlineData("/connect/userinfo")]
    [InlineData("/health")]
    [InlineData("/.well-known/openid-configuration")]
    [InlineData("/.well-known/jwks")]
    [InlineData("/scim/v2")]
    [InlineData("/scim/v2/Users")]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/hangfire")]
    [InlineData("/hangfire/dashboard")]
    public async Task InvokeAsync_SkipListPath_PassesThroughUnchanged(string path)
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = path;

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().Be(path);
        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_RootPath_PassesThroughUnchanged()
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = "/";

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().Be("/");
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
    public async Task InvokeAsync_EmptyPath_PassesThroughUnchanged()
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = new PathString(string.Empty);

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().BeNullOrEmpty();
        _nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/value", "/v1/value")]
    [InlineData("/versions", "/v1/versions")]
    public async Task InvokeAsync_PathStartingWithVButNoDigit_RewritesToV1(string original, string expected)
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = original;

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().Be(expected);
        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext()
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = "/identity/users";

        await sut.InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/CONNECT/TOKEN")]
    [InlineData("/Connect/Token")]
    [InlineData("/Health")]
    [InlineData("/HEALTH")]
    [InlineData("/Hangfire")]
    public async Task InvokeAsync_SkipListPath_CaseInsensitive_PassesThroughUnchanged(string path)
    {
        ApiVersionRewriteMiddleware sut = new(_next);
        DefaultHttpContext context = new();
        context.Request.Path = path;

        await sut.InvokeAsync(context);

        context.Request.Path.Value.Should().Be(path);
        _nextCalled.Should().BeTrue();
    }
}
