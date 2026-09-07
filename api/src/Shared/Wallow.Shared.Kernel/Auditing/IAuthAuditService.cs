namespace Wallow.Shared.Kernel.Auditing;

public record AuthAuditRecord
{
    public required string EventType { get; init; }
    /// <summary>
    /// The event subject, or null for events without a person, such as client authentication failures.
    /// </summary>
    public required Guid? UserId { get; init; }
    /// <summary>
    /// The actor responsible for an administrative or membership change. May equal UserId
    /// for self-service membership and client lifecycle events; authentication events omit it.
    /// </summary>
    public Guid? ActorId { get; init; }
    /// <summary>
    /// The organization context, or null for events outside an organization.
    /// </summary>
    public Guid? TenantId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    /// <summary>
    /// The client involved in the event, when applicable.
    /// </summary>
    public string? ClientId { get; init; }
    /// <summary>
    /// The operator's reason for a platform suspension; absent when lifting the suspension.
    /// </summary>
    public string? Reason { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public interface IAuthAuditService
{
    Task RecordAsync(AuthAuditRecord record, CancellationToken ct);
}
