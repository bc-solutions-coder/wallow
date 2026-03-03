using Foundry.Messaging.IntegrationTests.Fixtures;
using Foundry.Messaging.IntegrationTests.Helpers;
using Foundry.Messaging.IntegrationTests.TestEvents;
using Foundry.Messaging.IntegrationTests.TestHandlers;
using Wolverine;

namespace Foundry.Messaging.IntegrationTests.Tests;

[Trait("Category", "Integration")]
public class MessageRetryTests : IClassFixture<MessagingTestFixture>, IAsyncLifetime
{
    private readonly MessagingTestFixture _fixture;

    public MessageRetryTests(MessagingTestFixture fixture)
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
    public async Task Should_Retry_Failed_Message_And_Eventually_Succeed()
    {
        IMessageBus bus = _fixture.MessageBus;
        MessageWaiter waiter = _fixture.MessageWaiter;
        IMessageTracker tracker = _fixture.MessageTracker;

        TestEventThatFails testEvent = new()
        {
            Message = "This will fail 2 times then succeed",
            FailAfterAttempts = 3
        };

        await bus.PublishAsync(testEvent);

        await waiter.WaitForAttemptCountAsync(testEvent.EventId, expectedAttempts: 3);

        int attemptCount = tracker.GetAttemptCount(testEvent.EventId);
        attemptCount.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Should_Handle_Transient_Errors_With_Retry()
    {
        IMessageBus bus = _fixture.MessageBus;
        MessageWaiter waiter = _fixture.MessageWaiter;
        IMessageTracker tracker = _fixture.MessageTracker;

        TestEventThatFails event1 = new()
        {
            Message = "First failing event",
            FailAfterAttempts = 2
        };

        TestEventThatFails event2 = new()
        {
            Message = "Second failing event",
            FailAfterAttempts = 3
        };

        await bus.PublishAsync(event1);
        await bus.PublishAsync(event2);

        await waiter.WaitForAttemptCountAsync(event1.EventId, expectedAttempts: 2);
        await waiter.WaitForAttemptCountAsync(event2.EventId, expectedAttempts: 3);

        tracker.GetAttemptCount(event1.EventId).Should().BeGreaterThanOrEqualTo(2);
        tracker.GetAttemptCount(event2.EventId).Should().BeGreaterThanOrEqualTo(3);
    }
}
