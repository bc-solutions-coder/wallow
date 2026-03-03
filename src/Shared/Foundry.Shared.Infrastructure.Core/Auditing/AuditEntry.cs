namespace Foundry.Shared.Infrastructure.Auditing;

public class AuditEntry
{
    public Guid Id { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public required string Action { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
