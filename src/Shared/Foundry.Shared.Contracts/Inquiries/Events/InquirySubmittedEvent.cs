namespace Foundry.Shared.Contracts.Inquiries.Events;

public sealed record InquirySubmittedEvent : IntegrationEvent
{
    public required Guid InquiryId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string? Company { get; init; }
    public string? Phone { get; init; }
    public required string Subject { get; init; }
    public required string Message { get; init; }
    public required DateTime SubmittedAt { get; init; }
}
