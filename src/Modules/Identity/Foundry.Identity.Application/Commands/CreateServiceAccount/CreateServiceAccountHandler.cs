using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Identity.Application.Commands.CreateServiceAccount;

public sealed class CreateServiceAccountHandler(IServiceAccountService serviceAccountService)
{
    public async Task<Result<ServiceAccountCreatedResult>> Handle(
        CreateServiceAccountCommand command,
        CancellationToken ct)
    {
        CreateServiceAccountRequest request = new(
            command.Name,
            command.Description,
            command.Scopes);

        ServiceAccountCreatedResult result = await serviceAccountService.CreateAsync(request, ct);
        return Result.Success(result);
    }
}
