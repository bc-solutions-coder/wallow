using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Queries.GetApiScopes;

public sealed class GetApiScopesHandler(IApiScopeRepository apiScopeRepository)
{
    public async Task<Result<IReadOnlyList<ApiScopeDto>>> Handle(
        GetApiScopesQuery query,
        CancellationToken ct)
    {
        IReadOnlyList<ApiScope> scopes = await apiScopeRepository.GetAllAsync(query.Category, ct);

        List<ApiScopeDto> dtos = scopes
            .Select(s => new ApiScopeDto(
                s.Id,
                s.Code,
                s.DisplayName,
                s.Category,
                s.Description,
                s.IsDefault))
            .ToList();

        return Result.Success<IReadOnlyList<ApiScopeDto>>(dtos);
    }
}
