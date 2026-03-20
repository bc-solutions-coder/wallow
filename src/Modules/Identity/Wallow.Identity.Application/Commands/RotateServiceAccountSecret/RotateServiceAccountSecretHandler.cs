using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Commands.RotateServiceAccountSecret;

public sealed class RotateServiceAccountSecretHandler(IServiceAccountService serviceAccountService)
{
    public async Task<Result<SecretRotatedResult>> Handle(
        RotateServiceAccountSecretCommand command,
        CancellationToken ct)
    {
        SecretRotatedResult result = await serviceAccountService.RotateSecretAsync(command.Id, ct);
        return Result.Success(result);
    }
}
