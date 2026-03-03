using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Messaging.IntegrationTests.Fixtures;
using Foundry.Messaging.IntegrationTests.Helpers;
using Foundry.Messaging.IntegrationTests.TestEvents;
using Foundry.Messaging.IntegrationTests.TestHandlers;
using Wolverine;

namespace Foundry.Messaging.IntegrationTests.Tests;

[Trait("Category", "Integration")]
public class MessagePublishConsumeTests : IClassFixture<MessagingTestFixture>, IAsyncLifetime
{
    private static readonly int[] _expectedCounters = [0, 1, 2, 3, 4];
    private readonly MessagingTestFixture _fixture;

    public MessagePublishConsumeTests(MessagingTestFixture fixture)
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
    public async Task Should_Publish_And_Consume_TestEvent()
    {
        IMessageBus bus = _fixture.MessageBus;
        MessageWaiter waiter = _fixture.MessageWaiter;
        IMessageTracker tracker = _fixture.MessageTracker;

        TestEvent testEvent = new()
        {
            Message = "Hello from test",
            Counter = 42
        };

        await bus.PublishAsync(testEvent);

        await waiter.WaitForEventCountAsync(expectedCount: 1);

        IReadOnlyList<TestEvent> processedEvents = tracker.GetProcessedEvents();
        processedEvents.Should().ContainSingle();
        processedEvents[0].Message.Should().Be("Hello from test");
        processedEvents[0].Counter.Should().Be(42);
    }

    [Fact]
    public async Task Should_Publish_IntegrationEvent_From_Contracts()
    {
        IMessageBus bus = _fixture.MessageBus;
        UserRegisteredEvent userRegisteredEvent = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };

        Func<Task> publishTask = async () => await bus.PublishAsync(userRegisteredEvent);

        await publishTask.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Should_Handle_Multiple_Messages_In_Sequence()
    {
        IMessageBus bus = _fixture.MessageBus;
        MessageWaiter waiter = _fixture.MessageWaiter;
        IMessageTracker tracker = _fixture.MessageTracker;

        for (int i = 0; i < 5; i++)
        {
            await bus.PublishAsync(new TestEvent
            {
                Message = $"Message {i}",
                Counter = i
            });
        }

        await waiter.WaitForEventCountAsync(expectedCount: 5);

        IReadOnlyList<TestEvent> processedEvents = tracker.GetProcessedEvents();
        processedEvents.Should().HaveCount(5);
        processedEvents.Select(e => e.Counter).Should().BeEquivalentTo(_expectedCounters);
    }
}
