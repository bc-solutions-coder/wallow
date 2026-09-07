using Wallow.Branding.Application.Interfaces;

namespace Wallow.Branding.Application.Exceptions;

/// <summary>
/// An <see cref="IClientBrandingRepository"/> insert violated uniqueness. Losing inserts
/// are detached so callers can re-fetch the winning row and retry on the same repository.
/// </summary>
public sealed class DuplicateClientBrandingException(string clientId, Exception innerException)
    : Exception($"A branding row for client '{clientId}' already exists.", innerException)
{
    public string ClientId { get; } = clientId;
}
