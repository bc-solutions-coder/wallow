namespace Wallow.Shared.Kernel.Identity;

/// <summary>
/// Interface for strongly-typed IDs. Prevents mixing up IDs of different entity types.
/// Each entity defines its own ID type that implements this interface.
/// </summary>
/// <example>
/// public readonly record struct UserId(Guid Value) : IStronglyTypedId;
/// public readonly record struct TaskId(Guid Value) : IStronglyTypedId;
/// </example>
public interface IStronglyTypedId
{
    Guid Value { get; }
}

/// <summary>
/// Generic interface for strongly-typed IDs with self-referencing type constraint.
/// Enables generic operations on ID types.
/// </summary>
public interface IStronglyTypedId<T> : IStronglyTypedId
    where T : struct, IStronglyTypedId<T>
{
    /// <summary>
    /// Creates a new instance of the ID with the given value.
    /// </summary>
    static abstract T Create(Guid value);

    /// <summary>
    /// Creates a new instance with a new Guid.
    /// </summary>
    static abstract T New();
}
