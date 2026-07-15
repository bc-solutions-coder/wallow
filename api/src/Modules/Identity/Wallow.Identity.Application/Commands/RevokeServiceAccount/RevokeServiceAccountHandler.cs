using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Commands.RevokeServiceAccount;

public sealed class RevokeServiceAccountHandler(IServiceAccountService serviceAccountService)
{
    public async Task<Result> Handle(
        RevokeServiceAccountCommand command,
        CancellationToken ct)
    {
        await serviceAccountService.RevokeAsync(command.Id, ct);
        return Result.Success();
    }
}
