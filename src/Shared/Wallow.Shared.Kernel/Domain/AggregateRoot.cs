using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.Domain;

/// <summary>
/// Base class for aggregate roots. Aggregate roots are the entry point to an aggregate
/// and are responsible for maintaining invariants. They can raise domain events.
/// </summary>
/// <typeparam name="TId">The strongly-typed ID type for this aggregate</typeparam>
public abstract class AggregateRoot<TId> : AuditableEntity<TId>
    where TId : struct, IStronglyTypedId<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Domain events raised by this aggregate. Cleared after persistence.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>
    /// Raises a domain event. Events are dispatched after the aggregate is persisted.
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all domain events. Called by infrastructure after events are dispatched.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
