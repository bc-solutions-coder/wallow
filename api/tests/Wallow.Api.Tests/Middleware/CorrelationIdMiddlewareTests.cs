using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Wallow.Api.Middleware;

namespace Wallow.Api.Tests.Middleware;

public sealed class CorrelationIdMiddlewareTests : IDisposable
{
    private readonly ActivitySource _activitySource = new("Wallow.Tests.CorrelationId");

    [Fact]
    public async Task InvokeAsync_WithExistingHeader_UsesProvidedCorrelationId()
    {
        string expectedId = "my-correlation-id";
        bool nextCalled = false;
        CorrelationIdMiddleware sut = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-Correlation-Id"] = expectedId;

        await sut.InvokeAsync(httpContext);

        httpContext.Response.Headers["X-Correlation-Id"].ToString().Should().Be(expectedId);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithoutHeader_GeneratesNewCorrelationId()
    {
        CorrelationIdMiddleware sut = new(_ => Task.CompletedTask);
        DefaultHttpContext httpContext = new();

        await sut.InvokeAsync(httpContext);

        string correlationId = httpContext.Response.Headers["X-Correlation-Id"].ToString();
        correlationId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SetsCorrelationIdOnResponseHeader()
    {
        CorrelationIdMiddleware sut = new(_ => Task.CompletedTask);
        DefaultHttpContext httpContext = new();

        await sut.InvokeAsync(httpContext);

        httpContext.Response.Headers.Should().ContainKey("X-Correlation-Id");
    }

    [Fact]
    public async Task InvokeAsync_WithActivity_SetsCorrelationIdTag()
    {
        using ActivityListener listener = CreateListener();
        using Activity? activity = _activitySource.StartActivity();
        activity.Should().NotBeNull();

        string expectedId = "tagged-correlation-id";
        CorrelationIdMiddleware sut = new(_ => Task.CompletedTask);
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-Correlation-Id"] = expectedId;

        await sut.InvokeAsync(httpContext);

        activity!.GetTagItem("wallow.correlation_id").Should().Be(expectedId);
    }

    [Fact]
    public async Task InvokeAsync_WithoutActivity_DoesNotThrow()
    {
        CorrelationIdMiddleware sut = new(_ => Task.CompletedTask);
        DefaultHttpContext httpContext = new();

        Func<Task> act = () => sut.InvokeAsync(httpContext);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNextDelegate()
    {
        bool nextCalled = false;
        CorrelationIdMiddleware sut = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext httpContext = new();

        await sut.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
    }

    private ActivityListener CreateListener()
    {
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == _activitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    public void Dispose()
    {
        _activitySource.Dispose();
    }
}
