using Foundry.Showcases.Domain.Entities;
using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;

namespace Foundry.Showcases.Application.Contracts;

public interface IShowcaseRepository
{
    Task<Showcase?> GetByIdAsync(ShowcaseId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Showcase>> GetAllAsync(ShowcaseCategory? category, string? tag, CancellationToken cancellationToken = default);

    Task AddAsync(Showcase showcase, CancellationToken cancellationToken = default);

    Task UpdateAsync(Showcase showcase, CancellationToken cancellationToken = default);

    Task DeleteAsync(ShowcaseId id, CancellationToken cancellationToken = default);
}
