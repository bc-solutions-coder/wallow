using Foundry.Inquiries.Domain.Events;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Inquiries.Domain.Entities;

public sealed class InquiryComment : AggregateRoot<InquiryCommentId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public InquiryId InquiryId { get; private set; }
    public string AuthorId { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public bool IsInternal { get; private set; }

    private InquiryComment() { } // EF Core

    public static InquiryComment Create(
        InquiryId inquiryId,
        string authorId,
        string authorName,
        string content,
        bool isInternal,
        TimeProvider timeProvider)
    {
        InquiryComment comment = new()
        {
            Id = InquiryCommentId.New(),
            InquiryId = inquiryId,
            AuthorId = authorId,
            AuthorName = authorName,
            Content = content,
            IsInternal = isInternal
        };

        comment.SetCreated(timeProvider.GetUtcNow());

        comment.RaiseDomainEvent(new InquiryCommentAddedDomainEvent(
            comment.Id.Value,
            inquiryId.Value,
            comment.TenantId.Value,
            authorId,
            isInternal));

        return comment;
    }
}
