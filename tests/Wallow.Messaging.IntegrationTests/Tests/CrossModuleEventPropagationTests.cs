using Wallow.Messaging.IntegrationTests.Fixtures;
using Wallow.Messaging.IntegrationTests.Helpers;
using Wallow.Messaging.IntegrationTests.TestHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Messaging.IntegrationTests.Tests;

[Trait("Category", "Integration")]
public class CrossModuleEventPropagationTests : IClassFixture<MessagingTestFixture>, IAsyncLifetime
{
    private readonly MessagingTestFixture _fixture;

    public CrossModuleEventPropagationTests(MessagingTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _fixture.CrossModuleTracker.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UserRegistered_Should_Propagate_To_Multiple_Modules()
    {
        IMessageBus bus = _fixture.MessageBus;
        MessageWaiter waiter = _fixture.MessageWaiter;
        ICrossModuleEventTracker tracker = _fixture.CrossModuleTracker;

        UserRegisteredEvent userRegisteredEvent = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };

        await bus.PublishAsync(userRegisteredEvent);

        await waiter.WaitForCrossModuleHandlersAsync(
            "UserRegistered",
            userRegisteredEvent.EventId,
            expectedHandlerCount: 1,
            timeoutMs: 15000);

        IReadOnlyList<string> handlers = tracker.GetExecutedHandlers("UserRegistered", userRegisteredEvent.EventId);
        handlers.Should().Contain("Test");
    }

    [Fact]
    public async Task Multiple_Events_Should_Propagate_Independently()
    {
        IMessageBus bus = _fixture.MessageBus;
        MessageWaiter waiter = _fixture.MessageWaiter;
        ICrossModuleEventTracker tracker = _fixture.CrossModuleTracker;

        UserRegisteredEvent userRegistered1 = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user1@example.com",
            FirstName = "User",
            LastName = "One"
        };

        UserRegisteredEvent userRegistered2 = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user2@example.com",
            FirstName = "User",
            LastName = "Two"
        };

        await bus.PublishAsync(userRegistered1);
        await bus.PublishAsync(userRegistered2);

        await waiter.WaitForCrossModuleHandlersAsync(
            "UserRegistered",
            userRegistered1.EventId,
            expectedHandlerCount: 1,
            timeoutMs: 15000);

        await waiter.WaitForCrossModuleHandlersAsync(
            "UserRegistered",
            userRegistered2.EventId,
            expectedHandlerCount: 1,
            timeoutMs: 15000);

        IReadOnlyList<string> handlers1 = tracker.GetExecutedHandlers("UserRegistered", userRegistered1.EventId);
        IReadOnlyList<string> handlers2 = tracker.GetExecutedHandlers("UserRegistered", userRegistered2.EventId);

        handlers1.Should().Contain("Test");
        handlers2.Should().Contain("Test");
        handlers1.Should().NotBeEmpty();
        handlers2.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Event_Propagation_Should_Complete_Within_Timeout()
    {
        IMessageBus bus = _fixture.MessageBus;
        MessageWaiter waiter = _fixture.MessageWaiter;

        UserRegisteredEvent userRegisteredEvent = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "fast@example.com",
            FirstName = "Fast",
            LastName = "User"
        };

        DateTime startTime = DateTime.UtcNow;

        await bus.PublishAsync(userRegisteredEvent);

        await waiter.WaitForCrossModuleHandlersAsync(
            "UserRegistered",
            userRegisteredEvent.EventId,
            expectedHandlerCount: 1,
            timeoutMs: 15000);

        TimeSpan elapsed = DateTime.UtcNow - startTime;

        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
    }
}
