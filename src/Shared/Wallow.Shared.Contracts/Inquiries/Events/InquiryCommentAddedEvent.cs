namespace Wallow.Shared.Contracts.Inquiries.Events;

public sealed record InquiryCommentAddedEvent : IntegrationEvent
{
    public required Guid InquiryCommentId { get; init; }
    public required Guid InquiryId { get; init; }
    public required Guid TenantId { get; init; }
    public required string AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public required bool IsInternal { get; init; }
    public string SubmitterEmail { get; init; } = string.Empty;
    public string SubmitterName { get; init; } = string.Empty;
    public Guid? SubmitterUserId { get; init; }
    public string InquirySubject { get; init; } = string.Empty;
    public string CommentContent { get; init; } = string.Empty;
}
