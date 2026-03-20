using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Queries.GetServiceAccount;

public sealed class GetServiceAccountHandler(IServiceAccountService serviceAccountService)
{
    public async Task<Result<ServiceAccountDto?>> Handle(
        GetServiceAccountQuery query,
        CancellationToken ct)
    {
        ServiceAccountDto? result = await serviceAccountService.GetAsync(query.Id, ct);
        return Result.Success(result);
    }
}
