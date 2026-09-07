namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Resolves access-request email recipients. No recipients is a valid empty result;
/// the saved pending membership remains the request record. Storage errors may still propagate.
/// </summary>
public interface IAccessRequestRecipientResolver
{
    Task<IReadOnlyList<string>> ResolveAsync(Guid organizationId, CancellationToken ct = default);
}
