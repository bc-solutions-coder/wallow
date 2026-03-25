using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class InitialAccessTokenRepository(IdentityDbContext context) : IInitialAccessTokenRepository
{
    public async Task AddAsync(InitialAccessToken token, CancellationToken ct)
    {
        context.InitialAccessTokens.Add(token);
        await context.SaveChangesAsync(ct);
    }

    public Task<InitialAccessToken?> GetByIdAsync(InitialAccessTokenId id, CancellationToken ct)
    {
        return context.InitialAccessTokens
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<InitialAccessToken?> GetByHashAsync(string tokenHash, CancellationToken ct)
    {
        return context.InitialAccessTokens
            .AsTracking()
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);
    }

    public Task<List<InitialAccessToken>> ListAsync(CancellationToken ct)
    {
        return context.InitialAccessTokens
            .OrderByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return context.SaveChangesAsync(ct);
    }
}
