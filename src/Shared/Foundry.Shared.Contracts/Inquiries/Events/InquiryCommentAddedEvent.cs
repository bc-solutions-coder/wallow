namespace Foundry.Shared.Contracts.Inquiries.Events;

public sealed record InquiryCommentAddedEvent : IntegrationEvent
{
    public required Guid InquiryCommentId { get; init; }
    public required Guid InquiryId { get; init; }
    public required Guid TenantId { get; init; }
    public required string AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public required bool IsInternal { get; init; }
}
