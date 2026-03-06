using Foundry.Showcases.Application.Contracts;
using Foundry.Showcases.Domain.Entities;
using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Showcases.Infrastructure.Persistence.Repositories;

public sealed class ShowcaseRepository : IShowcaseRepository
{
    private static readonly Func<ShowcasesDbContext, ShowcaseId, CancellationToken, Task<Showcase?>> _getByIdQuery =
        EF.CompileAsyncQuery(
            (ShowcasesDbContext ctx, ShowcaseId id, CancellationToken _) =>
                ctx.Showcases.AsTracking().FirstOrDefault(s => s.Id == id));

    private readonly ShowcasesDbContext _context;

    public ShowcaseRepository(ShowcasesDbContext context)
    {
        _context = context;
    }

    public Task<Showcase?> GetByIdAsync(ShowcaseId id, CancellationToken cancellationToken = default)
    {
        return _getByIdQuery(_context, id, cancellationToken);
    }

    public async Task<IReadOnlyList<Showcase>> GetAllAsync(
        ShowcaseCategory? category,
        string? tag,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Showcase> query = _context.Showcases.AsQueryable();

        if (category.HasValue)
        {
            query = query.Where(s => s.Category == category.Value);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            query = query.Where(s => EF.Property<List<string>>(s, "_tags").Contains(tag));
        }

        return await query
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Showcase showcase, CancellationToken cancellationToken = default)
    {
        _context.Showcases.Add(showcase);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Showcase showcase, CancellationToken cancellationToken = default)
    {
        _context.Showcases.Update(showcase);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ShowcaseId id, CancellationToken cancellationToken = default)
    {
        Showcase? showcase = await _getByIdQuery(_context, id, cancellationToken);
        if (showcase is not null)
        {
            _context.Showcases.Remove(showcase);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
