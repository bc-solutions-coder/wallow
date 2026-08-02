namespace Wallow.Shared.Kernel.Auditing;

public record AuthAuditRecord
{
    public required string EventType { get; init; }
    public required Guid UserId { get; init; }
    /// <summary>
    /// Who caused the event, when that is somebody other than the subject. Null for the events a
    /// person triggers about themselves, which is every authentication event: nobody logs in on
    /// somebody else's behalf. An administrative decision about a member has an actor and the
    /// subject is not it, which is the question this column exists to answer.
    /// </summary>
    public Guid? ActorId { get; init; }
    /// <summary>
    /// The organization the event happened inside, or null when it happened outside every
    /// organization. Authentication is something a person does, not something an organization
    /// does: which organization they act in is settled later, by the token they are issued.
    /// </summary>
    public Guid? TenantId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public interface IAuthAuditService
{
    Task RecordAsync(AuthAuditRecord record, CancellationToken ct);
}
