using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Interfaces;

public interface IRegistrationValidator
{
    Task<Result> ValidateAsync(string email, string? displayName, CancellationToken ct);
}
