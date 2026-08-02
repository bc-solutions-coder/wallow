namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// First-run wizard input. The organization is part of it because roles are granted per
/// organization: without one the new administrator would hold no permission anywhere.
/// </summary>
public sealed record CreateAdminRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string OrganizationName);
