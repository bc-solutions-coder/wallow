using System.Diagnostics;
using Wallow.Api.Middleware;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;

namespace Wallow.Api.Tests.Middleware;

public sealed class TenantBaggageMiddlewareTests : IDisposable
{
    private readonly ActivitySource _activitySource = new("Wallow.Tests.TenantBaggage");

    [Fact]
    public async Task InvokeAsync_WhenTenantResolved_SetsBaggageWithTenantId()
    {
        TenantId tenantId = TenantId.New();
        ITenantContext tenantContext = CreateResolvedTenantContext(tenantId);
        string? capturedBaggage = null;

        // Baggage is set via AsyncLocal and only visible downstream (in next delegate), not upstream to the caller.
        TenantBaggageMiddleware sut = new(_ =>
        {
            capturedBaggage = Baggage.GetBaggage("wallow.tenant_id");
            return Task.CompletedTask;
        });
        DefaultHttpContext httpContext = new();

        await sut.InvokeAsync(httpContext, tenantContext);

        capturedBaggage.Should().Be(tenantId.Value.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenTenantResolved_SetsActivityTag()
    {
        using ActivityListener listener = CreateListener();
        using Activity? activity = _activitySource.StartActivity();
        activity.Should().NotBeNull();

        TenantId tenantId = TenantId.New();
        ITenantContext tenantContext = CreateResolvedTenantContext(tenantId);
        TenantBaggageMiddleware sut = new(_ => Task.CompletedTask);
        DefaultHttpContext httpContext = new();

        await sut.InvokeAsync(httpContext, tenantContext);

        activity!.GetTagItem("wallow.tenant_id").Should().Be(tenantId.Value.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenTenantNotResolved_DoesNotSetActivityTag()
    {
        using ActivityListener listener = CreateListener();
        using Activity? activity = _activitySource.StartActivity();
        activity.Should().NotBeNull();

        ITenantContext tenantContext = CreateUnresolvedTenantContext();
        TenantBaggageMiddleware sut = new(_ => Task.CompletedTask);
        DefaultHttpContext httpContext = new();

        await sut.InvokeAsync(httpContext, tenantContext);

        activity!.GetTagItem("wallow.tenant_id").Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNextDelegate()
    {
        bool nextCalled = false;
        TenantBaggageMiddleware sut = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        ITenantContext tenantContext = CreateUnresolvedTenantContext();
        DefaultHttpContext httpContext = new();

        await sut.InvokeAsync(httpContext, tenantContext);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenTenantNotResolved_StillCallsNext()
    {
        bool nextCalled = false;
        TenantBaggageMiddleware sut = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        ITenantContext tenantContext = CreateUnresolvedTenantContext();
        DefaultHttpContext httpContext = new();

        await sut.InvokeAsync(httpContext, tenantContext);

        nextCalled.Should().BeTrue();
    }

    private static ITenantContext CreateResolvedTenantContext(TenantId tenantId)
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(true);
        tenantContext.TenantId.Returns(tenantId);
        return tenantContext;
    }

    private static ITenantContext CreateUnresolvedTenantContext()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(false);
        return tenantContext;
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
        Baggage.ClearBaggage();
        _activitySource.Dispose();
    }
}
