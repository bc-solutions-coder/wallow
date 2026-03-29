using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Shared.Kernel.Auditing;

namespace Wallow.Shared.Infrastructure.Core.Auditing;

public sealed partial class AuthAuditService(
    IDbContextFactory<AuthAuditDbContext> contextFactory,
    ILogger<AuthAuditService> logger) : IAuthAuditService
{
    public async Task RecordAsync(AuthAuditRecord record, CancellationToken ct)
    {
        try
        {
            await using AuthAuditDbContext context = await contextFactory.CreateDbContextAsync(ct);

            AuthAuditEntry entry = new()
            {
                Id = Guid.NewGuid(),
                EventType = record.EventType,
                UserId = record.UserId,
                TenantId = record.TenantId,
                IpAddress = record.IpAddress,
                UserAgent = record.UserAgent,
                OccurredAt = record.OccurredAt
            };

            context.AuthAuditEntries.Add(entry);
            await context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            LogAuditWriteFailed(logger, record.EventType, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to write auth audit record for event {EventType}")]
    private static partial void LogAuditWriteFailed(ILogger logger, string eventType, Exception ex);
}
