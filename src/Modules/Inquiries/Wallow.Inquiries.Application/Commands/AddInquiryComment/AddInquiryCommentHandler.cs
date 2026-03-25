using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Inquiries.Application.Commands.AddInquiryComment;

public static class AddInquiryCommentHandler
{
    public static async Task<Result<InquiryCommentId>> HandleAsync(
        AddInquiryCommentCommand command,
        IInquiryCommentRepository commentRepository,
        IInquiryRepository inquiryRepository,
        IMessageBus bus,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        InquiryComment comment = InquiryComment.Create(
            command.InquiryId,
            command.AuthorId,
            command.AuthorName,
            command.Content,
            command.IsInternal,
            new TenantId(command.TenantId),
            timeProvider);

        await commentRepository.AddAsync(comment, cancellationToken);
        await commentRepository.SaveChangesAsync(cancellationToken);

        Inquiry? inquiry = await inquiryRepository.GetByIdAsync(command.InquiryId, cancellationToken);

        Guid? submitterUserId = inquiry?.SubmitterId is not null
            && Guid.TryParse(inquiry.SubmitterId, out Guid parsed)
                ? parsed
                : null;

        await bus.PublishAsync(new InquiryCommentAddedEvent
        {
            InquiryCommentId = comment.Id.Value,
            InquiryId = command.InquiryId.Value,
            TenantId = command.TenantId,
            AuthorId = command.AuthorId,
            AuthorName = command.AuthorName,
            IsInternal = command.IsInternal,
            SubmitterEmail = inquiry?.Email ?? string.Empty,
            SubmitterName = inquiry?.Name ?? string.Empty,
            SubmitterUserId = submitterUserId,
            InquirySubject = inquiry?.ProjectType ?? string.Empty,
            CommentContent = command.Content
        });

        return Result.Success(comment.Id);
    }
}
