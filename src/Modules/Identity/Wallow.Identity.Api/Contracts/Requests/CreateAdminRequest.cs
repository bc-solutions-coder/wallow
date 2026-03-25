namespace Wallow.Identity.Api.Contracts.Requests;

public sealed record CreateAdminRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName);
