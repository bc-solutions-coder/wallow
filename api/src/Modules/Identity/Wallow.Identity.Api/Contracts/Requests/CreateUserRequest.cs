namespace Wallow.Identity.Api.Contracts.Requests;

public record CreateUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string? Password);
