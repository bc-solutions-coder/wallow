namespace Foundry.Shared.Contracts.Inquiries.Events;

public sealed record InquiryStatusChangedEvent : IntegrationEvent
{
    public required Guid InquiryId { get; init; }
    public required string OldStatus { get; init; }
    public required string NewStatus { get; init; }
    public required DateTime ChangedAt { get; init; }
    public required string SubmitterEmail { get; init; }
}
