using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Shared.Kernel.Tests.Domain;

public class AggregateRootTests
{
    [Fact]
    public void RaiseDomainEvent_AddEventToDomainEvents()
    {
        TestAggregate aggregate = new(TestAggregateId.New());
        TestDomainEvent testEvent = new();

        aggregate.RaiseTestEvent(testEvent);

        aggregate.DomainEvents.Should().ContainSingle();
        aggregate.DomainEvents.Should().Contain(testEvent);
    }

    [Fact]
    public void RaiseDomainEvent_MultipleEvents_AddsAllEvents()
    {
        TestAggregate aggregate = new(TestAggregateId.New());
        TestDomainEvent event1 = new();
        TestDomainEvent event2 = new();
        TestDomainEvent event3 = new();

        aggregate.RaiseTestEvent(event1);
        aggregate.RaiseTestEvent(event2);
        aggregate.RaiseTestEvent(event3);

        aggregate.DomainEvents.Should().HaveCount(3);
        aggregate.DomainEvents.Should().ContainInOrder(event1, event2, event3);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        TestAggregate aggregate = new(TestAggregateId.New());
        aggregate.RaiseTestEvent(new TestDomainEvent());
        aggregate.RaiseTestEvent(new TestDomainEvent());

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_WhenNoEvents_DoesNotThrow()
    {
        TestAggregate aggregate = new(TestAggregateId.New());

        Action act = () => aggregate.ClearDomainEvents();

        act.Should().NotThrow();
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_IsReadOnly()
    {
        TestAggregate aggregate = new(TestAggregateId.New());
        aggregate.RaiseTestEvent(new TestDomainEvent());

        aggregate.DomainEvents.Should().BeAssignableTo<IReadOnlyList<IDomainEvent>>();
    }

    [Fact]
    public void DomainEvents_InitiallyEmpty()
    {
        TestAggregate aggregate = new(TestAggregateId.New());

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RaiseDomainEvent_AfterClear_CanAddNewEvents()
    {
        TestAggregate aggregate = new(TestAggregateId.New());
        aggregate.RaiseTestEvent(new TestDomainEvent());
        aggregate.ClearDomainEvents();

        TestDomainEvent newEvent = new();
        aggregate.RaiseTestEvent(newEvent);

        aggregate.DomainEvents.Should().ContainSingle();
        aggregate.DomainEvents.Should().Contain(newEvent);
    }

    [Fact]
    public void InheritsFromAuditableEntity()
    {
        TestAggregate aggregate = new(TestAggregateId.New());

        aggregate.Should().BeAssignableTo<AuditableEntity<TestAggregateId>>();
    }

    [Fact]
    public void InheritsFromEntity()
    {
        TestAggregate aggregate = new(TestAggregateId.New());

        aggregate.Should().BeAssignableTo<Entity<TestAggregateId>>();
    }

    [Fact]
    public void ParameterlessCtor_InitializesWithDefaultId()
    {
        TestAggregate aggregate = new();

        aggregate.Id.Should().Be(default(TestAggregateId));
        aggregate.DomainEvents.Should().BeEmpty();
    }

    private sealed class TestAggregate : AggregateRoot<TestAggregateId>
    {
        public TestAggregate() { }
        public TestAggregate(TestAggregateId id) : base(id) { }

        public void RaiseTestEvent(IDomainEvent domainEvent)
        {
            RaiseDomainEvent(domainEvent);
        }
    }

    private readonly record struct TestAggregateId(Guid Value) : IStronglyTypedId<TestAggregateId>
    {
        public static TestAggregateId Create(Guid value) => new(value);
        public static TestAggregateId New() => new(Guid.NewGuid());
    }

    private sealed record TestDomainEvent : DomainEvent;
}
