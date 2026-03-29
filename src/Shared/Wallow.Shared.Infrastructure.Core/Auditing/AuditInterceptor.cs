using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wallow.Shared.Kernel.Auditing;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Shared.Infrastructure.Core.Auditing;

public sealed partial class AuditInterceptor(
    IServiceProvider serviceProvider,
    ILogger<AuditInterceptor> logger) : SaveChangesInterceptor
{

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null or AuditDbContext)
        {
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        List<AuditEntry> auditEntries = CaptureChanges(eventData.Context);

        InterceptionResult<int> interceptionResult = await base.SavingChangesAsync(eventData, result, cancellationToken);

        if (auditEntries.Count > 0)
        {
            await SaveAuditEntriesAsync(auditEntries, cancellationToken);
        }

        return interceptionResult;
    }

    private List<AuditEntry> CaptureChanges(DbContext context)
    {
        List<AuditEntry> entries = [];

        string? userId = null;
        Guid? tenantId = null;

        try
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            IHttpContextAccessor? httpContextAccessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();
            userId = httpContextAccessor?.HttpContext?.User.GetUserId();

            ITenantContext? tenantContext = scope.ServiceProvider.GetService<ITenantContext>();
            if (tenantContext is { IsResolved: true })
            {
                tenantId = tenantContext.TenantId.Value;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            LogContextResolutionFailed(ex);
        }

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        foreach (EntityEntry entry in context.ChangeTracker.Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is AuditEntry)
            {
                continue;
            }

            string entityType = entry.Entity.GetType().Name;
            string entityId = GetPrimaryKeyValue(entry);
            string action = entry.State switch
            {
                EntityState.Added => "Insert",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => "Unknown"
            };

            string? oldValues = entry.State is EntityState.Modified or EntityState.Deleted
                ? SerializeValues(entry.OriginalValues)
                : null;

            string? newValues = entry.State is EntityState.Added or EntityState.Modified
                ? SerializeValues(entry.CurrentValues)
                : null;

            entries.Add(new AuditEntry
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                OldValues = oldValues,
                NewValues = newValues,
                UserId = userId,
                TenantId = tenantId,
                Timestamp = timestamp
            });
        }

        return entries;
    }

    private async Task SaveAuditEntriesAsync(List<AuditEntry> entries, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        AuditDbContext auditDbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        auditDbContext.AuditEntries.AddRange(entries);
        await auditDbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetPrimaryKeyValue(EntityEntry entry)
    {
        IReadOnlyList<IProperty>? keyProperties = entry.Metadata.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count == 0)
        {
            return string.Empty;
        }

        if (keyProperties.Count == 1)
        {
            return Convert.ToString(entry.Property(keyProperties[0].Name).CurrentValue, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        IEnumerable<string> keyValues = keyProperties.Select(p => Convert.ToString(entry.Property(p.Name).CurrentValue, CultureInfo.InvariantCulture) ?? string.Empty);
        return string.Join(",", keyValues);
    }

    private static string SerializeValues(PropertyValues propertyValues)
    {
        Dictionary<string, object?> dict = new();
        foreach (IProperty property in propertyValues.Properties)
        {
            PropertyInfo? propertyInfo = property.PropertyInfo;
            if (propertyInfo?.GetCustomAttribute<AuditIgnoreAttribute>() != null)
            {
                continue;
            }

            dict[property.Name] = propertyValues[property];
        }
        return JsonSerializer.Serialize(dict);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to resolve audit context services (e.g., during migrations)")]
    private partial void LogContextResolutionFailed(Exception exception);
}
