namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// First-run administrator credentials and the organization in which to grant ownership.
/// </summary>
public sealed record CreateAdminRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string OrganizationName);
