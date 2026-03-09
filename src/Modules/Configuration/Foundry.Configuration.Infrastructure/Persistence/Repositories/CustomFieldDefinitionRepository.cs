using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Configuration.Infrastructure.Persistence.Repositories;

public sealed class CustomFieldDefinitionRepository(ConfigurationDbContext context) : ICustomFieldDefinitionRepository
{

    public Task<CustomFieldDefinition?> GetByIdAsync(
        CustomFieldDefinitionId id,
        CancellationToken cancellationToken = default)
    {
        return context.CustomFieldDefinitions
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomFieldDefinition>> GetByEntityTypeAsync(
        string entityType,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<CustomFieldDefinition> query = context.CustomFieldDefinitions
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

    public Task<bool> FieldKeyExistsAsync(
        string entityType,
        string fieldKey,
        CancellationToken cancellationToken = default)
    {
        return context.CustomFieldDefinitions
            .AnyAsync(x => x.EntityType == entityType && x.FieldKey == fieldKey, cancellationToken);
    }

    public async Task AddAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default)
    {
        await context.CustomFieldDefinitions.AddAsync(definition, cancellationToken);
    }

    public Task UpdateAsync(CustomFieldDefinition definition, CancellationToken cancellationToken = default)
    {
        context.CustomFieldDefinitions.Update(definition);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
