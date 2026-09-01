using Wallow.Branding.Application.Interfaces;

namespace Wallow.Branding.Application.Exceptions;

/// <summary>
/// Raised by <see cref="IClientBrandingRepository.SaveChangesAsync"/> when an insert loses a race
/// on the repo-wide client_id unique index — a concurrent writer created the row between the
/// caller's existence check and the save. The losing insert has been detached, so the caller can
/// re-fetch the winning row on the same repository and apply its write as an update.
/// </summary>
public sealed class DuplicateClientBrandingException(string clientId, Exception innerException)
    : Exception($"A branding row for client '{clientId}' already exists.", innerException)
{
    public string ClientId { get; } = clientId;
}
