using System.Text.RegularExpressions;

namespace Wallow.Shared.Kernel.Errors;

/// <summary>
/// One entry of an error catalog: the code a client keys on, the kind that decides the HTTP
/// status, and the user-safe sentence a response carries when the raising site supplies none.
/// </summary>
/// <remarks>
/// Entries are the only way to construct an <see cref="Results.Error"/> or a
/// <see cref="Domain.DomainException"/>, so every code that can reach a client is declared in
/// exactly one catalog and reaches the OpenAPI document from there. A code is dotted PascalCase
/// <c>Area.Reason</c>; the constructor refuses anything else so a malformed code fails the
/// catalog's static initialiser instead of leaking into a response.
/// </remarks>
public sealed partial record ErrorCatalogEntry
{
    /// <summary>
    /// Creates an entry.
    /// </summary>
    /// <param name="code">The dotted PascalCase <c>Area.Reason</c> code.</param>
    /// <param name="kind">The kind that decides the HTTP status.</param>
    /// <param name="defaultMessage">The user-safe sentence used when no override is supplied.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="code"/> is not <c>Area.Reason</c>, or <paramref name="defaultMessage"/> is empty.
    /// </exception>
    public ErrorCatalogEntry(string code, ErrorKind kind, string defaultMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultMessage);

        if (!CodePattern().IsMatch(code))
        {
            throw new ArgumentException(
                $"Error code '{code}' must be dotted PascalCase 'Area.Reason' (for example 'Storage.QuotaExceeded').",
                nameof(code));
        }

        Code = code;
        Kind = kind;
        DefaultMessage = defaultMessage;
    }

    /// <summary>Gets the machine-readable code clients key on.</summary>
    public string Code { get; }

    /// <summary>Gets the kind that decides the HTTP status.</summary>
    public ErrorKind Kind { get; }

    /// <summary>Gets the user-safe sentence a response carries when the raising site supplies none.</summary>
    public string DefaultMessage { get; }

    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CodePattern();
}
