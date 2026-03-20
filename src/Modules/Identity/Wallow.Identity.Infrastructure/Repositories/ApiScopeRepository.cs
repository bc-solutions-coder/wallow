using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class ApiScopeRepository(IdentityDbContext context) : IApiScopeRepository
{

    public async Task<IReadOnlyList<ApiScope>> GetAllAsync(string? category = null, CancellationToken ct = default)
    {
        IQueryable<ApiScope> query = context.ApiScopes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        return await query
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApiScope>> GetByCodesAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        List<string> codeList = codes.ToList();
        return await context.ApiScopes
            .Where(x => codeList.Contains(x.Code))
            .ToListAsync(ct);
    }

    public void Add(ApiScope scope)
    {
        context.ApiScopes.Add(scope);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await context.SaveChangesAsync(ct);
    }
}
