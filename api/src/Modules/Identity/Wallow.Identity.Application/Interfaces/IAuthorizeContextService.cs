using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Resolves public branding, organization and requested scopes for a serviceable client
/// with a registered redirect URI. A matching URI is a lookup condition, not proof
/// that an authorization transaction exists.
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
