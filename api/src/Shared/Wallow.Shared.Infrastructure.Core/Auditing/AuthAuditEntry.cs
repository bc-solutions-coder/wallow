namespace Wallow.Shared.Infrastructure.Core.Auditing;

public class AuthAuditEntry
{
    public Guid Id { get; set; }
    public required string EventType { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
