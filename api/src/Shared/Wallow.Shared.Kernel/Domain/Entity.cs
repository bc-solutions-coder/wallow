using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.Domain;

/// <summary>
/// Base class for all entities. Entities have identity and are compared by ID.
/// Uses strongly-typed IDs to prevent mixing up IDs of different entity types.
/// </summary>
/// <typeparam name="TId">The strongly-typed ID type for this entity</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : struct, IStronglyTypedId<TId>
{
    /// <summary>
    /// The unique identifier for this entity.
    /// </summary>
    public TId Id { get; protected init; }

    /// <summary>
    /// Parameterless constructor for EF Core materialization.
    /// </summary>
    protected Entity() { }

    /// <summary>
    /// Constructor for creating new entities with a specific ID.
    /// </summary>
    protected Entity(TId id)
    {
        Id = id;
    }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity<TId> entity && Equals(entity);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !Equals(left, right);
    }
}
