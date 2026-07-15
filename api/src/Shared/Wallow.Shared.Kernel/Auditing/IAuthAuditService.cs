namespace Wallow.Shared.Kernel.Auditing;

public record AuthAuditRecord
{
    public required string EventType { get; init; }
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public interface IAuthAuditService
{
    Task RecordAsync(AuthAuditRecord record, CancellationToken ct);
}
