using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.Domain;

/// <summary>
/// Tracks creation and modification timestamps and actors.
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
    /// Sets the creation timestamp in UTC and the optional actor.
    /// </summary>
    public void SetCreated(DateTimeOffset timestamp, Guid? userId = null)
    {
        CreatedAt = timestamp.UtcDateTime;
        CreatedBy = userId;
    }

    /// <summary>
    /// Sets the update timestamp in UTC and the optional actor.
    /// </summary>
    public void SetUpdated(DateTimeOffset timestamp, Guid? userId = null)
    {
        UpdatedAt = timestamp.UtcDateTime;
        UpdatedBy = userId;
    }
}
