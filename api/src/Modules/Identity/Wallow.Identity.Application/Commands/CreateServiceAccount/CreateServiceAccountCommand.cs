namespace Wallow.Identity.Application.Commands.CreateServiceAccount;

public sealed record CreateServiceAccountCommand(
    string Name,
    string? Description,
    IEnumerable<string> Scopes);
