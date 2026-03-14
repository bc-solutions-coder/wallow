using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Application.Commands.AddInquiryComment;

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
