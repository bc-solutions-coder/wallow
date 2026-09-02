using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Kernel.Results;

/// <summary>
/// The failure half of a <see cref="Result"/>: a catalog entry's code and kind, with the entry's
/// default sentence or an override from the raising site.
/// </summary>
/// <remarks>
/// An error is only ever built from an <see cref="ErrorCatalogEntry"/>, so the code is one the
/// aggregated catalog exports and the kind, not the code's text, decides the HTTP status.
/// </remarks>
public sealed record Error
{
    /// <summary>The absence of an error, carried by every successful result.</summary>
    public static readonly Error None = new(string.Empty, ErrorKind.Failure, string.Empty);

    private Error(string code, ErrorKind kind, string message)
    {
        Code = code;
        Kind = kind;
        Message = message;
    }

    /// <summary>
    /// Creates an error from a catalog entry.
    /// </summary>
    /// <param name="entry">The catalog entry.</param>
    /// <param name="message">
    /// A sentence replacing the entry's default, for a site that can say more than the catalog
    /// does. Keep it user-safe: it becomes the response's <c>detail</c>.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="message"/> is empty.</exception>
    public Error(ErrorCatalogEntry entry, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (message is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
        }

        Code = entry.Code;
        Kind = entry.Kind;
        Message = message ?? entry.DefaultMessage;
    }

    /// <summary>Gets the machine-readable code.</summary>
    public string Code { get; }

    /// <summary>Gets the kind that decides the HTTP status.</summary>
    public ErrorKind Kind { get; }

    /// <summary>Gets the user-safe sentence.</summary>
    public string Message { get; }
}
