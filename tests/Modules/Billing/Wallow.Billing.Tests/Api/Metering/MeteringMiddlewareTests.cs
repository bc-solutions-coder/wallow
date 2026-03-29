using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Wallow.Billing.Api.Middleware;
using Wallow.Billing.Application.Metering.DTOs;
using Wallow.Billing.Application.Metering.Services;
using Wallow.Billing.Domain.Metering.Enums;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Fakes;
using Wolverine;

namespace Wallow.Billing.Tests.Api.Metering;

public class MeteringMiddlewareTests
{
    private readonly IMeteringService _meteringService;
    private readonly ITenantContext _tenantContext;
    private readonly IMessageBus _messageBus;
    private readonly MeteringMiddleware _middleware;
    private bool _nextCalled;

    public MeteringMiddlewareTests()
    {
        _meteringService = Substitute.For<IMeteringService>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));
        _messageBus = Substitute.For<IMessageBus>();
        _nextCalled = false;
        RequestDelegate next = _ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        };
        HybridCache cache = new NoOpHybridCache();
        _middleware = new MeteringMiddleware(next, cache);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/swagger")]
    [InlineData("/metrics")]
    [InlineData("/")]
    public async Task InvokeAsync_NonApiRoute_ShouldSkipMetering(string path)
    {
        DefaultHttpContext context = new() { Request = { Path = path } };

        await _middleware.InvokeAsync(context, _meteringService, _tenantContext, _messageBus);

        _nextCalled.Should().BeTrue();
        await _meteringService.DidNotReceive().CheckQuotaAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task InvokeAsync_QuotaExceeded_ShouldReturn429()
    {
        DefaultHttpContext context = new() { Request = { Path = "/api/users" } };
        context.Response.Body = new MemoryStream();

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(
                IsAllowed: false,
                CurrentUsage: 1001,
                Limit: 1000,
                PercentUsed: 100.1m,
                ActionIfExceeded: QuotaAction.Block));

        await _middleware.InvokeAsync(context, _meteringService, _tenantContext, _messageBus);

        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        context.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("1000");
        context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("0");
        context.Response.Headers.Should().ContainKey("Retry-After");
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_QuotaExceededWithWarnAction_ShouldAllowRequest()
    {
        DefaultHttpContext context = new() { Request = { Path = "/api/users" } };
        context.Response.Body = new MemoryStream();

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(
                IsAllowed: false,
                CurrentUsage: 1001,
                Limit: 1000,
                PercentUsed: 100.1m,
                ActionIfExceeded: QuotaAction.Warn));

        await _middleware.InvokeAsync(context, _meteringService, _tenantContext, _messageBus);

        _nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().NotBe(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task InvokeAsync_SuccessfulRequest_ShouldIncrementCounter()
    {
        DefaultHttpContext context = new() { Request = { Path = "/api/users" } };
        context.Response.StatusCode = 200;

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(
                IsAllowed: true,
                CurrentUsage: 500,
                Limit: 1000,
                PercentUsed: 50,
                ActionIfExceeded: null));

        await _middleware.InvokeAsync(context, _meteringService, _tenantContext, _messageBus);

        await _messageBus.Received(1).SendAsync(Arg.Any<object>());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task InvokeAsync_FailedRequest_ShouldNotIncrementCounter(int statusCode)
    {
        DefaultHttpContext context = new() { Request = { Path = "/api/users" } };
        context.Response.StatusCode = statusCode;

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(
                IsAllowed: true,
                CurrentUsage: 500,
                Limit: 1000,
                PercentUsed: 50,
                ActionIfExceeded: null));

        await _middleware.InvokeAsync(context, _meteringService, _tenantContext, _messageBus);

        await _messageBus.DidNotReceive().SendAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task InvokeAsync_Above80Percent_ShouldAddWarningHeader()
    {
        DefaultHttpContext context = new() { Request = { Path = "/api/users" } };
        context.Response.StatusCode = 200;

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(
                IsAllowed: true,
                CurrentUsage: 850,
                Limit: 1000,
                PercentUsed: 85,
                ActionIfExceeded: null));

        await _middleware.InvokeAsync(context, _meteringService, _tenantContext, _messageBus);

        context.Response.Headers["X-Quota-Warning"].ToString().Should().Contain("85%");
    }

    [Fact]
    public async Task InvokeAsync_Below80Percent_ShouldNotAddWarningHeader()
    {
        DefaultHttpContext context = new() { Request = { Path = "/api/users" } };
        context.Response.StatusCode = 200;

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(
                IsAllowed: true,
                CurrentUsage: 500,
                Limit: 1000,
                PercentUsed: 50,
                ActionIfExceeded: null));

        await _middleware.InvokeAsync(context, _meteringService, _tenantContext, _messageBus);

        context.Response.Headers.Should().NotContainKey("X-Quota-Warning");
    }

    [Fact]
    public async Task InvokeAsync_WithQuota_ShouldAddRateLimitHeaders()
    {
        DefaultHttpContext context = new() { Request = { Path = "/api/users" } };
        context.Response.StatusCode = 200;

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(
                IsAllowed: true,
                CurrentUsage: 500,
                Limit: 1000,
                PercentUsed: 50,
                ActionIfExceeded: null));

        await _middleware.InvokeAsync(context, _meteringService, _tenantContext, _messageBus);

        context.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("1000");
        context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("499");
        context.Response.Headers.Should().ContainKey("X-RateLimit-Reset");
    }

    [Fact]
    public async Task InvokeAsync_WithUnlimitedQuota_ShouldNotAddRateLimitHeaders()
    {
        DefaultHttpContext context = new() { Request = { Path = "/api/users" } };
        context.Response.StatusCode = 200;

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(new QuotaCheckResult(
                IsAllowed: true,
                CurrentUsage: 500,
                Limit: decimal.MaxValue,
                PercentUsed: 0,
                ActionIfExceeded: null));

        await _middleware.InvokeAsync(context, _meteringService, _tenantContext, _messageBus);

        context.Response.Headers.Should().NotContainKey("X-RateLimit-Limit");
        context.Response.Headers.Should().NotContainKey("X-RateLimit-Remaining");
    }

    [Fact]
    public async Task InvokeAsync_ShouldCheckQuotaBeforeProcessing()
    {
        DefaultHttpContext context = new() { Request = { Path = "/api/users" } };
        bool checkQuotaCalled = false;

        _meteringService.CheckQuotaAsync("api.calls")
            .Returns(_ =>
            {
                checkQuotaCalled = true;
                _nextCalled.Should().BeFalse("CheckQuotaAsync should be called before next delegate");
                return new QuotaCheckResult(true, 0, 1000, 0, null);
            });

        await _middleware.InvokeAsync(context, _meteringService, _tenantContext, _messageBus);

        checkQuotaCalled.Should().BeTrue();
        _nextCalled.Should().BeTrue();
    }
}
