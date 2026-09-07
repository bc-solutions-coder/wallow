using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.Domain;

/// <summary>
/// Compares entities by their strongly typed ID.
/// </summary>
/// <typeparam name="TId">The strongly-typed ID type for this entity</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : struct, IStronglyTypedId<TId>
{
    public TId Id { get; protected init; }

    /// <summary>
    /// Parameterless constructor for EF Core materialization.
    /// </summary>
    protected Entity() { }

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
