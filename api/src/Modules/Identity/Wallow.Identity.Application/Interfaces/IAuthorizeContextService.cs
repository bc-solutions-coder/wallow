using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Describes the client behind a pending authorize transaction to the auth host: branding,
/// owning organization and requested scopes, resolved only for a client that could actually be
/// mid-transaction. The redirect URI acts as the proof of a genuine transaction — a caller who
/// cannot present one the client registered learns nothing, so client ids cannot be enumerated.
/// </summary>
public interface IAuthorizeContextService
{
    /// <summary>
    /// The transaction context, or <see langword="null"/> when the client is unknown, the
    /// redirect URI is not one it registered, or the client is currently refused service —
    /// indistinguishable on purpose.
    /// </summary>
    Task<AuthorizeContextDto?> ResolveAsync(
        string clientId,
        string redirectUri,
        IReadOnlyCollection<string> requestedScopes,
        CancellationToken cancellationToken = default);
}
