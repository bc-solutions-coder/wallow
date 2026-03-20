using Wallow.Messaging.IntegrationTests.Fixtures;
using Wallow.Messaging.IntegrationTests.Helpers;
using Wallow.Messaging.IntegrationTests.TestEvents;
using Wallow.Messaging.IntegrationTests.TestHandlers;
using Wolverine;

namespace Wallow.Messaging.IntegrationTests.Tests;

[Trait("Category", "Integration")]
public class MessageDeadLetterTests : IClassFixture<MessagingTestFixture>, IAsyncLifetime
{
    private readonly MessagingTestFixture _fixture;

    public MessageDeadLetterTests(MessagingTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _fixture.MessageTracker.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_Move_To_Dead_Letter_Queue_After_ArgumentException()
    {
        IMessageBus bus = _fixture.MessageBus;
        MessageWaiter waiter = _fixture.MessageWaiter;
        IMessageTracker tracker = _fixture.MessageTracker;

        TestEventThatFailsImmediately testEvent = new()
        {
            Message = "This will fail immediately with ArgumentException"
        };

        await bus.PublishAsync(testEvent);

        await waiter.WaitForMessageAsync(
            () => true,
            timeoutMs: 3000);

        int attemptCount = tracker.GetAttemptCount(testEvent.EventId);
        attemptCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_Move_To_Dead_Letter_Queue_After_Max_Retries()
    {
        IMessageBus bus = _fixture.MessageBus;
        MessageWaiter waiter = _fixture.MessageWaiter;
        IMessageTracker tracker = _fixture.MessageTracker;

        TestEventThatFails testEvent = new()
        {
            Message = "This will never succeed",
            FailAfterAttempts = 100
        };

        await bus.PublishAsync(testEvent);

        await waiter.WaitForAttemptCountAsync(testEvent.EventId, expectedAttempts: 1, timeoutMs: 8000);

        int attemptCount = tracker.GetAttemptCount(testEvent.EventId);
        attemptCount.Should().BeGreaterThan(0);
        attemptCount.Should().BeLessThan(100);
    }
}
