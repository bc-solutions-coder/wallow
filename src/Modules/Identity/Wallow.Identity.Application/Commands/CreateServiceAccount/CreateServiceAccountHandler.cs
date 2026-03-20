using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Commands.CreateServiceAccount;

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
