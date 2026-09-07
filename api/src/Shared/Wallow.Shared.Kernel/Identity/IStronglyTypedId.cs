namespace Wallow.Shared.Kernel.Identity;

/// <summary>
/// Common Guid value for entity-specific ID types.
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
/// Static factories for an entity-specific ID type.
/// </summary>
public interface IStronglyTypedId<T> : IStronglyTypedId
    where T : struct, IStronglyTypedId<T>
{
    /// <summary>
    /// Wraps the supplied Guid.
    /// </summary>
    static abstract T Create(Guid value);

    /// <summary>
    /// Creates an ID with a new Guid.
    /// </summary>
    static abstract T New();
}
