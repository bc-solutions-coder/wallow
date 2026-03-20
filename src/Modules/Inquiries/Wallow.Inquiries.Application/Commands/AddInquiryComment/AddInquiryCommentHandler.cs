using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Application.Commands.AddInquiryComment;

public static class AddInquiryCommentHandler
{
    public static async Task<Result<InquiryCommentId>> HandleAsync(
        AddInquiryCommentCommand command,
        IInquiryCommentRepository commentRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        InquiryComment comment = InquiryComment.Create(
            command.InquiryId,
            command.AuthorId,
            command.AuthorName,
            command.Content,
            command.IsInternal,
            timeProvider);

        await commentRepository.AddAsync(comment, cancellationToken);

        return Result.Success(comment.Id);
    }
}
