using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.Domain;

/// <summary>
/// Auditable aggregate with a collection of raised domain events.
/// </summary>
/// <typeparam name="TId">The strongly-typed ID type for this aggregate</typeparam>
public abstract class AggregateRoot<TId> : AuditableEntity<TId>
    where TId : struct, IStronglyTypedId<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Events collected by this aggregate until explicitly cleared.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>
    /// Collects an event; this method does not dispatch it.
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears the collected events.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
