using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Queries.GetServiceAccounts;

public sealed class GetServiceAccountsHandler(IServiceAccountService serviceAccountService)
{
    public async Task<Result<IReadOnlyList<ServiceAccountDto>>> Handle(
        GetServiceAccountsQuery _,
        CancellationToken ct)
    {
        IReadOnlyList<ServiceAccountDto> result = await serviceAccountService.ListAsync(ct);
        return Result.Success(result);
    }
}
