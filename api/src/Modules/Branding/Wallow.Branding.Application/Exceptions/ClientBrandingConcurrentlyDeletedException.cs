using Wallow.Branding.Application.Interfaces;

namespace Wallow.Branding.Application.Exceptions;

/// <summary>
/// Raised by <see cref="IClientBrandingRepository.SaveChangesAsync"/> when a tracked row was
/// deleted underneath the save — the client itself was removed while a caller was writing its
/// branding. The stale entries have been detached, so the repository remains usable; callers
/// answer as if the row never existed.
/// </summary>
public sealed class ClientBrandingConcurrentlyDeletedException(string clientId, Exception innerException)
    : Exception($"The branding row for client '{clientId}' was deleted concurrently.", innerException)
{
    public string ClientId { get; } = clientId;
}
