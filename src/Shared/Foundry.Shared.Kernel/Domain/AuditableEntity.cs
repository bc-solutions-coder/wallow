using Foundry.Shared.Kernel.Identity;

namespace Foundry.Shared.Kernel.Domain;

/// <summary>
/// Base class for entities that track creation and modification metadata.
/// Inherit from this when you need audit trails.
/// </summary>
/// <typeparam name="TId">The strongly-typed ID type for this entity</typeparam>
public abstract class AuditableEntity<TId> : Entity<TId>
    where TId : struct, IStronglyTypedId<TId>
{
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public Guid? CreatedBy { get; protected set; }
    public Guid? UpdatedBy { get; protected set; }

    protected AuditableEntity() { }

    protected AuditableEntity(TId id) : base(id) { }

    /// <summary>
    /// Sets the creation audit fields. Call this when creating a new entity.
    /// </summary>
    public void SetCreated(DateTimeOffset timestamp, Guid? userId = null)
    {
        CreatedAt = timestamp.UtcDateTime;
        CreatedBy = userId;
    }

    /// <summary>
    /// Sets the update audit fields. Call this when modifying an entity.
    /// </summary>
    public void SetUpdated(DateTimeOffset timestamp, Guid? userId = null)
    {
        UpdatedAt = timestamp.UtcDateTime;
        UpdatedBy = userId;
    }
}
