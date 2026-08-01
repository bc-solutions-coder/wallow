namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Who is told that somebody asked to join an organization.
/// </summary>
/// <remarks>
/// An interface so the enrollment service names no Infrastructure type. Resolution never fails a
/// request: the pending membership is the durable record and the email is a convenience, so an
/// organization with nobody to tell yields an empty list rather than an exception.
/// </remarks>
public interface IAccessRequestRecipientResolver
{
    Task<IReadOnlyList<string>> ResolveAsync(Guid organizationId, CancellationToken ct = default);
}
