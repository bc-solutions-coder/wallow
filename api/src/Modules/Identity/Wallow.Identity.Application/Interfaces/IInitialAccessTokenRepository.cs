using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.Interfaces;

public interface IInitialAccessTokenRepository
{
    Task AddAsync(InitialAccessToken token, CancellationToken ct);
    Task<InitialAccessToken?> GetByIdAsync(InitialAccessTokenId id, CancellationToken ct);
    Task<InitialAccessToken?> GetByHashAsync(string tokenHash, CancellationToken ct);
    Task<List<InitialAccessToken>> ListAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
