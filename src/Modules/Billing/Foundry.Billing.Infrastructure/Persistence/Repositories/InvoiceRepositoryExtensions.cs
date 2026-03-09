using System.Text.Json;
using Foundry.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Persistence.Repositories;

public static class InvoiceRepositoryExtensions
{
    /// <summary>
    /// Find invoices by a custom field value.
    /// Uses GIN index for efficient querying.
    /// </summary>
    public static async Task<IReadOnlyList<Invoice>> FindByCustomFieldAsync(
        this DbSet<Invoice> invoices,
        string fieldKey,
        string fieldValue,
        CancellationToken cancellationToken = default)
    {
        string jsonFilter = JsonSerializer.Serialize(new Dictionary<string, string> { { fieldKey, fieldValue } });

        return await invoices
            .Where(i => i.CustomFields != null && EF.Functions.JsonContains(i.CustomFields, jsonFilter))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Find invoices matching multiple custom field criteria.
    /// </summary>
    public static async Task<IReadOnlyList<Invoice>> FindByCustomFieldsAsync(
        this DbSet<Invoice> invoices,
        Dictionary<string, string> criteria,
        CancellationToken cancellationToken = default)
    {
        string jsonFilter = JsonSerializer.Serialize(criteria);

        return await invoices
            .Where(i => i.CustomFields != null && EF.Functions.JsonContains(i.CustomFields, jsonFilter))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Check if any invoice has a specific custom field value.
    /// Useful for uniqueness validation.
    /// </summary>
    public static Task<bool> CustomFieldValueExistsAsync(
        this DbSet<Invoice> invoices,
        string fieldKey,
        string fieldValue,
        CancellationToken cancellationToken = default)
    {
        string jsonFilter = JsonSerializer.Serialize(new Dictionary<string, string> { { fieldKey, fieldValue } });

        return invoices
            .AnyAsync(i => i.CustomFields != null && EF.Functions.JsonContains(i.CustomFields, jsonFilter), cancellationToken);
    }
}
