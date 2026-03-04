using System.Net.Sockets;
using Microsoft.Extensions.Time.Testing;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Foundry.Communications.Tests.Infrastructure.Services;

public class SmtpResilienceTests
{
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan _baseDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _totalTimeout = TimeSpan.FromSeconds(30);

    private static ResiliencePipeline BuildSmtpPipeline(FakeTimeProvider fakeTime)
    {
        ResiliencePipelineBuilder builder = new()
        {
            TimeProvider = fakeTime
        };

        return builder
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = _baseDelay,
                UseJitter = false
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = _totalTimeout
            })
            .Build();
    }

    private static void AdvanceTimeInBackground(FakeTimeProvider fakeTime, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                fakeTime.Advance(TimeSpan.FromSeconds(3));
            }
        }, cancellationToken);
    }

    [Fact]
    public async Task SmtpPipeline_WhenAllAttemptsFail_RetriesExactlyMaxRetryAttempts()
    {
        FakeTimeProvider fakeTime = new();
        int attemptCount = 0;
        ResiliencePipeline pipeline = BuildSmtpPipeline(fakeTime);

        using CancellationTokenSource cts = new();
        AdvanceTimeInBackground(fakeTime, cts.Token);

        Func<Task> act = async () =>
        {
            await pipeline.ExecuteAsync(async _ =>
            {
                attemptCount++;
                throw new SocketException((int)SocketError.ConnectionRefused);
            });
        };

        await act.Should().ThrowAsync<SocketException>();
        await cts.CancelAsync();

        int expectedTotalAttempts = MaxRetryAttempts + 1; // 1 initial + 3 retries
        attemptCount.Should().Be(expectedTotalAttempts,
            $"pipeline should execute 1 initial attempt + {MaxRetryAttempts} retries = {expectedTotalAttempts} total");
    }

    [Fact]
    public async Task SmtpPipeline_WhenSecondAttemptSucceeds_StopsRetrying()
    {
        FakeTimeProvider fakeTime = new();
        int attemptCount = 0;
        ResiliencePipeline pipeline = BuildSmtpPipeline(fakeTime);

        using CancellationTokenSource cts = new();
        AdvanceTimeInBackground(fakeTime, cts.Token);

        await pipeline.ExecuteAsync(async _ =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new SocketException((int)SocketError.ConnectionRefused);
            }
        });

        await cts.CancelAsync();

        attemptCount.Should().Be(2, "pipeline should stop retrying after the second attempt succeeds");
    }
}
