namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Who is told when the platform suspends an organization or one of its clients: the active
/// owners' email addresses.
/// </summary>
/// <remarks>
/// An interface so the callers name no Infrastructure type. Resolution never fails the
/// suspension: the suspension itself is the durable record and the email is a courtesy, so an
/// organization with nobody to tell yields an empty list rather than an exception.
/// </remarks>
public interface IOrganizationAdminEmailResolver
{
    Task<IReadOnlyList<string>> ResolveAsync(Guid organizationId, CancellationToken ct = default);
}
