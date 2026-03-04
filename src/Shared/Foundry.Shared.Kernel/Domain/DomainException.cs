#pragma warning disable CA1032 // Intentionally removing weak constructors to enforce structured exception creation

namespace Foundry.Shared.Kernel.Domain;

/// <summary>
/// Base class for domain exceptions. These represent business rule violations.
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>
    /// Machine-readable error code for client handling.
    /// </summary>
    public string Code { get; } = string.Empty;

    protected DomainException(string code, string message) : base(message)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentException.ThrowIfNullOrEmpty(message);
        Code = code;
    }

    protected DomainException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentException.ThrowIfNullOrEmpty(message);
        Code = code;
    }

    protected DomainException(string message) : base(message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
    }

    protected DomainException(string message, Exception innerException) : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
    }
}

/// <summary>
/// Exception thrown when an entity is not found.
/// </summary>
public class EntityNotFoundException : DomainException
{
    public string EntityName { get; } = string.Empty;
    public object EntityId { get; } = Guid.Empty;

    public EntityNotFoundException(string entityName, object entityId)
        : base($"{entityName}.NotFound", $"{entityName} with ID '{entityId}' was not found")
    {
        EntityName = entityName;
        EntityId = entityId;
    }
}

/// <summary>
/// Exception thrown when a business rule is violated.
/// </summary>
public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string code, string message)
        : base(code, message)
    {
    }
}

public class ForbiddenAccessException : DomainException
{
    public ForbiddenAccessException(string message)
        : base("Access.Forbidden", message)
    {
    }
}
