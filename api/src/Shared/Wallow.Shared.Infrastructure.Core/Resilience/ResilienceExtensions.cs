using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;

namespace Wallow.Shared.Infrastructure.Core.Resilience;

public static class ResilienceExtensions
{
    private static readonly Action<ILogger, int, string, double, Exception?> _logRetry =
        LoggerMessage.Define<int, string, double>(
            LogLevel.Warning,
            new EventId(1, "ResilienceRetry"),
            "Resilience retry {AttemptNumber} for profile {ProfileName}. Delay: {RetryDelayMs}ms");

    private static readonly Action<ILogger, string, double, Exception?> _logCircuitBreakerOpened =
        LoggerMessage.Define<string, double>(
            LogLevel.Error,
            new EventId(2, "CircuitBreakerOpened"),
            "Circuit breaker opened for profile {ProfileName}. Break duration: {BreakDurationSeconds}s");

    private static readonly Action<ILogger, string, Exception?> _logCircuitBreakerClosed =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3, "CircuitBreakerClosed"),
            "Circuit breaker closed for profile {ProfileName}");

    private static readonly Action<ILogger, string, Exception?> _logCircuitBreakerHalfOpened =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(4, "CircuitBreakerHalfOpened"),
            "Circuit breaker half-opened for profile {ProfileName}");

    private static readonly ResiliencePropertyKey<ILogger?> _loggerKey = new("Wallow.Logger");

    public static IHttpClientBuilder AddWallowResilienceHandler(
        this IHttpClientBuilder builder,
        string profileName = "default")
    {
        builder.AddStandardResilienceHandler(options =>
        {
            ConfigureProfile(options, profileName);
            ConfigureLogging(options, profileName);
        });

        return builder;
    }

    private static void ConfigureProfile(HttpStandardResilienceOptions options, string profileName)
    {
        switch (profileName)
        {
            case "identity-provider":
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromMilliseconds(200);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 10;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                break;

            case "external-api":
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.FromMilliseconds(500);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.CircuitBreaker.FailureRatio = 0.3;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(60);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                break;

            case "health-check":
                options.Retry.MaxRetryAttempts = 1;
                options.Retry.Delay = TimeSpan.FromMilliseconds(500);
                options.Retry.BackoffType = DelayBackoffType.Constant;
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                break;

            default:
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromMilliseconds(100);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                break;
        }
    }

    private static void ConfigureLogging(HttpStandardResilienceOptions options, string profileName)
    {
        options.Retry.OnRetry = args =>
        {
            if (args.Context.Properties.TryGetValue(_loggerKey, out ILogger? logger) && logger is not null)
            {
                _logRetry(logger, args.AttemptNumber, profileName, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception);
            }

            return ValueTask.CompletedTask;
        };

        options.CircuitBreaker.OnOpened = args =>
        {
            if (args.Context.Properties.TryGetValue(_loggerKey, out ILogger? logger) && logger is not null)
            {
                _logCircuitBreakerOpened(logger, profileName, args.BreakDuration.TotalSeconds, args.Outcome.Exception);
            }

            return ValueTask.CompletedTask;
        };

        options.CircuitBreaker.OnClosed = args =>
        {
            if (args.Context.Properties.TryGetValue(_loggerKey, out ILogger? logger) && logger is not null)
            {
                _logCircuitBreakerClosed(logger, profileName, null);
            }

            return ValueTask.CompletedTask;
        };

        options.CircuitBreaker.OnHalfOpened = args =>
        {
            if (args.Context.Properties.TryGetValue(_loggerKey, out ILogger? logger) && logger is not null)
            {
                _logCircuitBreakerHalfOpened(logger, profileName, null);
            }

            return ValueTask.CompletedTask;
        };
    }
}
