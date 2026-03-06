using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using NSubstitute.Core;
using Foundry.Shared.Infrastructure.Core.Resilience;

#pragma warning disable CA1873
#pragma warning disable CA2000

namespace Foundry.Shared.Infrastructure.Tests.Resilience;

public class ResilienceExtensionsLoggingTests
{
    private static readonly ResiliencePropertyKey<ILogger?> _loggerKey = new("Foundry.Logger");

    private readonly ILogger _logger;

    public ResilienceExtensionsLoggingTests()
    {
        _logger = Substitute.For<ILogger>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task OnRetry_LogsWarningWithCorrectEventId()
    {
        HttpStandardResilienceOptions options = BuildResilienceOptions("default");
        ResilienceContext context = CreateContextWithLogger();

        await options.Retry.OnRetry!(new OnRetryArguments<HttpResponseMessage>(
            context,
            Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            attemptNumber: 1,
            retryDelay: TimeSpan.FromMilliseconds(200),
            duration: TimeSpan.FromMilliseconds(200)));

        ResilienceContextPool.Shared.Return(context);

        ICall logCall = _logger.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(ILogger.Log));

        LogLevel level = (LogLevel)logCall.GetArguments()[0]!;
        EventId eventId = (EventId)logCall.GetArguments()[1]!;

        level.Should().Be(LogLevel.Warning);
        eventId.Id.Should().Be(1);
        eventId.Name.Should().Be("ResilienceRetry");
    }

    [Fact]
    public async Task OnRetry_LogsProfileNameAndAttemptNumber()
    {
        HttpStandardResilienceOptions options = BuildResilienceOptions("external-api");
        ResilienceContext context = CreateContextWithLogger();

        await options.Retry.OnRetry!(new OnRetryArguments<HttpResponseMessage>(
            context,
            Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            attemptNumber: 2,
            retryDelay: TimeSpan.FromMilliseconds(500),
            duration: TimeSpan.FromMilliseconds(500)));

        ResilienceContextPool.Shared.Return(context);

        string message = GetLogMessage(_logger);
        message.Should().Contain("external-api");
        message.Should().Contain("2");
    }

    [Fact]
    public async Task OnCircuitBreakerOpened_LogsErrorWithCorrectEventId()
    {
        HttpStandardResilienceOptions options = BuildResilienceOptions("health-check");
        ResilienceContext context = CreateContextWithLogger();

        await options.CircuitBreaker.OnOpened!(new OnCircuitOpenedArguments<HttpResponseMessage>(
            context,
            Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            breakDuration: TimeSpan.FromSeconds(30),
            isManual: false));

        ResilienceContextPool.Shared.Return(context);

        ICall logCall = _logger.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(ILogger.Log));

        LogLevel level = (LogLevel)logCall.GetArguments()[0]!;
        EventId eventId = (EventId)logCall.GetArguments()[1]!;

        level.Should().Be(LogLevel.Error);
        eventId.Id.Should().Be(2);
        eventId.Name.Should().Be("CircuitBreakerOpened");
    }

    [Fact]
    public async Task OnCircuitBreakerOpened_LogsProfileNameAndBreakDuration()
    {
        HttpStandardResilienceOptions options = BuildResilienceOptions("identity-provider");
        ResilienceContext context = CreateContextWithLogger();

        await options.CircuitBreaker.OnOpened!(new OnCircuitOpenedArguments<HttpResponseMessage>(
            context,
            Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            breakDuration: TimeSpan.FromSeconds(30),
            isManual: false));

        ResilienceContextPool.Shared.Return(context);

        string message = GetLogMessage(_logger);
        message.Should().Contain("identity-provider");
        message.Should().Contain("30");
    }

    [Fact]
    public async Task OnCircuitBreakerClosed_LogsInformationWithCorrectEventId()
    {
        HttpStandardResilienceOptions options = BuildResilienceOptions("default");
        ResilienceContext context = CreateContextWithLogger();

        await options.CircuitBreaker.OnClosed!(new OnCircuitClosedArguments<HttpResponseMessage>(
            context,
            Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.OK)),
            isManual: false));

        ResilienceContextPool.Shared.Return(context);

        ICall logCall = _logger.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(ILogger.Log));

        LogLevel level = (LogLevel)logCall.GetArguments()[0]!;
        EventId eventId = (EventId)logCall.GetArguments()[1]!;

        level.Should().Be(LogLevel.Information);
        eventId.Id.Should().Be(3);
        eventId.Name.Should().Be("CircuitBreakerClosed");
    }

    [Fact]
    public async Task OnCircuitBreakerClosed_LogsProfileName()
    {
        HttpStandardResilienceOptions options = BuildResilienceOptions("external-api");
        ResilienceContext context = CreateContextWithLogger();

        await options.CircuitBreaker.OnClosed!(new OnCircuitClosedArguments<HttpResponseMessage>(
            context,
            Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.OK)),
            isManual: false));

        ResilienceContextPool.Shared.Return(context);

        string message = GetLogMessage(_logger);
        message.Should().Contain("external-api");
    }

    [Fact]
    public async Task OnCircuitBreakerHalfOpened_LogsInformationWithCorrectEventId()
    {
        HttpStandardResilienceOptions options = BuildResilienceOptions("default");
        ResilienceContext context = CreateContextWithLogger();

        await options.CircuitBreaker.OnHalfOpened!(new OnCircuitHalfOpenedArguments(context));

        ResilienceContextPool.Shared.Return(context);

        ICall logCall = _logger.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(ILogger.Log));

        LogLevel level = (LogLevel)logCall.GetArguments()[0]!;
        EventId eventId = (EventId)logCall.GetArguments()[1]!;

        level.Should().Be(LogLevel.Information);
        eventId.Id.Should().Be(4);
        eventId.Name.Should().Be("CircuitBreakerHalfOpened");
    }

    [Fact]
    public async Task OnCircuitBreakerHalfOpened_LogsProfileName()
    {
        HttpStandardResilienceOptions options = BuildResilienceOptions("identity-provider");
        ResilienceContext context = CreateContextWithLogger();

        await options.CircuitBreaker.OnHalfOpened!(new OnCircuitHalfOpenedArguments(context));

        ResilienceContextPool.Shared.Return(context);

        string message = GetLogMessage(_logger);
        message.Should().Contain("identity-provider");
    }

    [Fact]
    public async Task OnRetry_WithNoLoggerInContext_DoesNotLog()
    {
        HttpStandardResilienceOptions options = BuildResilienceOptions("default");
        ResilienceContext context = ResilienceContextPool.Shared.Get();

        await options.Retry.OnRetry!(new OnRetryArguments<HttpResponseMessage>(
            context,
            Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            attemptNumber: 1,
            retryDelay: TimeSpan.FromMilliseconds(200),
            duration: TimeSpan.FromMilliseconds(200)));

        ResilienceContextPool.Shared.Return(context);

        _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task OnCircuitBreakerOpened_WithNoLoggerInContext_DoesNotLog()
    {
        HttpStandardResilienceOptions options = BuildResilienceOptions("default");
        ResilienceContext context = ResilienceContextPool.Shared.Get();

        await options.CircuitBreaker.OnOpened!(new OnCircuitOpenedArguments<HttpResponseMessage>(
            context,
            Outcome.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            breakDuration: TimeSpan.FromSeconds(30),
            isManual: false));

        ResilienceContextPool.Shared.Return(context);

        _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log))
            .Should().BeEmpty();
    }

    private static HttpStandardResilienceOptions BuildResilienceOptions(string profileName)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHttpClient("test-client")
            .AddFoundryResilienceHandler(profileName);

        ServiceProvider provider = services.BuildServiceProvider();
        IOptionsMonitor<HttpStandardResilienceOptions> optionsMonitor =
            provider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();

        return optionsMonitor.Get("test-client-standard");
    }

    private ResilienceContext CreateContextWithLogger()
    {
        ResilienceContext context = ResilienceContextPool.Shared.Get();
        context.Properties.Set(_loggerKey, _logger);
        return context;
    }

    private static string GetLogMessage(ILogger logger)
    {
        ICall logCall = logger.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(ILogger.Log));

        object?[] args = logCall.GetArguments();
        object state = args[2]!;
        Exception? exception = args[3] as Exception;

        // The formatter is the 5th argument (index 4), but it's typed as Func<TState, Exception?, string>
        // We need to invoke it via reflection since TState is internal
        System.Reflection.MethodInfo toStringMethod = state.GetType().GetMethod("ToString")!;
        return (string)toStringMethod.Invoke(state, null)!;
    }
}
