using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Configuration.Infrastructure.Persistence.Repositories;

public sealed class CustomFieldDefinitionRepository : ICustomFieldDefinitionRepository
{
    private readonly ConfigurationDbContext _context;

    public CustomFieldDefinitionRepository(ConfigurationDbContext context)
    {
        _context = context;
    }

    public Task<CustomFieldDefinition?> GetByIdAsync(
        CustomFieldDefinitionId id,
        CancellationToken cancellationToken = default)
    {
        return _context.CustomFieldDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<CustomFieldDefinition?> GetByFieldKeyAsync(
        string entityType,
        string fieldKey,
        CancellationToken cancellationToken = default)
    {
        return _context.CustomFieldDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(x => x.EntityType == entityType && x.FieldKey == fieldKey, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomFieldDefinition>> GetByEntityTypeAsync(
        string entityType,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<CustomFieldDefinition> query = _context.CustomFieldDefinitions
            .Where(x => x.EntityType == entityType);

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomFieldDefinition>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.CustomFieldDefinitions
            .Where(x => x.IsActive)
            .OrderBy(x => x.EntityType)
            .ThenBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> FieldKeyExistsAsync(
        string entityType,
        string fieldKey,
        CancellationToken cancellationToken = default)
    {
        return _context.CustomFieldDefinitions
            .AnyAsync(x => x.EntityType == entityType && x.FieldKey == fieldKey, cancellationToken);
    }

    public async Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default)
    {
        await _context.CustomFieldDefinitions.AddAsync(definition, cancellationToken);
    }

    public Task UpdateAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default)
    {
        _context.CustomFieldDefinitions.Update(definition);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
