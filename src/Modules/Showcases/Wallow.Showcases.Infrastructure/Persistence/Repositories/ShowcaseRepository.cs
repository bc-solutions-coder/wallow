using Wallow.Showcases.Application.Contracts;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;
using Wallow.Showcases.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Showcases.Infrastructure.Persistence.Repositories;

public sealed class ShowcaseRepository(ShowcasesDbContext context) : IShowcaseRepository
{
    private static readonly Func<ShowcasesDbContext, ShowcaseId, CancellationToken, Task<Showcase?>> _getByIdQuery =
        EF.CompileAsyncQuery(
            (ShowcasesDbContext ctx, ShowcaseId id, CancellationToken _) =>
                ctx.Showcases.AsTracking().FirstOrDefault(s => s.Id == id));

    public Task<Showcase?> GetByIdAsync(ShowcaseId id, CancellationToken cancellationToken = default)
    {
        return _getByIdQuery(context, id, cancellationToken);
    }

    public async Task<IReadOnlyList<Showcase>> GetAllAsync(
        ShowcaseCategory? category,
        string? tag,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Showcase> query = context.Showcases.AsQueryable();

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
        context.Showcases.Add(showcase);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Showcase showcase, CancellationToken cancellationToken = default)
    {
        context.Showcases.Update(showcase);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ShowcaseId id, CancellationToken cancellationToken = default)
    {
        Showcase? showcase = await _getByIdQuery(context, id, cancellationToken);
        if (showcase is not null)
        {
            context.Showcases.Remove(showcase);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
