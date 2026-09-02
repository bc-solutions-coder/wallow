using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Kernel.Domain;

/// <summary>
/// Base class for exceptions that carry a catalogued error to the client. The entry's kind decides
/// the HTTP status; its code is the response's <c>code</c>; its default sentence is the
/// <c>detail</c> unless the raising site overrides it.
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>
    /// Creates the exception from a catalog entry.
    /// </summary>
    /// <param name="entry">The catalog entry.</param>
    /// <param name="message">A user-safe sentence replacing the entry's default, or null to keep it.</param>
    /// <param name="innerException">The cause, if any.</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is empty.</exception>
    protected DomainException(ErrorCatalogEntry entry, string? message = null, Exception? innerException = null)
        : base(ResolveMessage(entry, message), innerException)
    {
        Entry = entry;
    }

    /// <summary>Gets the catalog entry this exception reports.</summary>
    public ErrorCatalogEntry Entry { get; }

    /// <summary>Gets the machine-readable error code.</summary>
    public string Code => Entry.Code;

    /// <summary>Gets the kind that decides the HTTP status.</summary>
    public ErrorKind Kind => Entry.Kind;

    private static string ResolveMessage(ErrorCatalogEntry entry, string? message)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (message is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
        }

        return message ?? entry.DefaultMessage;
    }

    /// <summary>
    /// Refuses an entry whose kind is not the one this exception type stands for, so the type
    /// and the status a client sees never disagree.
    /// </summary>
    protected static ErrorCatalogEntry RequireKind(ErrorCatalogEntry entry, ErrorKind kind)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Kind != kind)
        {
            throw new ArgumentException(
                $"Entry '{entry.Code}' is of kind {entry.Kind}, but this exception carries {kind} entries.",
                nameof(entry));
        }

        return entry;
    }
}

/// <summary>
/// The target of the request does not exist or is not visible to the caller (404).
/// </summary>
public sealed class EntityNotFoundException : DomainException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="entry">A <see cref="ErrorKind.NotFound"/> entry.</param>
    /// <param name="entityId">The identifier that was looked up; kept for logging, never sent.</param>
    /// <param name="message">A user-safe sentence replacing the entry's default, or null to keep it.</param>
    public EntityNotFoundException(ErrorCatalogEntry entry, object entityId, string? message = null)
        : base(RequireKind(entry, ErrorKind.NotFound), message)
    {
        EntityId = entityId;
    }

    /// <summary>Gets the identifier that was looked up.</summary>
    public object EntityId { get; }
}

/// <summary>
/// The request was well-formed but a business rule refuses it (422).
/// </summary>
public class BusinessRuleException : DomainException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="entry">A <see cref="ErrorKind.BusinessRule"/> entry.</param>
    /// <param name="message">A user-safe sentence replacing the entry's default, or null to keep it.</param>
    public BusinessRuleException(ErrorCatalogEntry entry, string? message = null)
        : base(RequireKind(entry, ErrorKind.BusinessRule), message)
    {
    }
}

/// <summary>
/// The caller is authenticated but not allowed to do this (403).
/// </summary>
public sealed class ForbiddenAccessException : DomainException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="entry">A <see cref="ErrorKind.Forbidden"/> entry.</param>
    /// <param name="message">A user-safe sentence replacing the entry's default, or null to keep it.</param>
    public ForbiddenAccessException(ErrorCatalogEntry entry, string? message = null)
        : base(RequireKind(entry, ErrorKind.Forbidden), message)
    {
    }
}
