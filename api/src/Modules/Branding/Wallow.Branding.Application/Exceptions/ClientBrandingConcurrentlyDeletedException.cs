using Wallow.Branding.Application.Interfaces;

namespace Wallow.Branding.Application.Exceptions;

/// <summary>
/// An <see cref="IClientBrandingRepository"/> save lost a tracked row to concurrent deletion.
/// Stale entries are detached so callers can return not found and reuse the repository.
/// </summary>
public sealed class ClientBrandingConcurrentlyDeletedException(string clientId, Exception innerException)
    : Exception($"The branding row for client '{clientId}' was deleted concurrently.", innerException)
{
    public string ClientId { get; } = clientId;
}
