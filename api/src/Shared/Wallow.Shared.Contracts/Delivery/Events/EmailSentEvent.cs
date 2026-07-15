namespace Wallow.Shared.Contracts.Delivery.Events;

/// <summary>
/// Published when an email is successfully sent.
/// </summary>
public sealed record EmailSentEvent : IntegrationEvent
{
    public required Guid EmailId { get; init; }
    public required Guid TenantId { get; init; }
    public required string ToAddress { get; init; }
    public required string Subject { get; init; }
    public required string TemplateName { get; init; }
    public required DateTime SentAt { get; init; }
}
