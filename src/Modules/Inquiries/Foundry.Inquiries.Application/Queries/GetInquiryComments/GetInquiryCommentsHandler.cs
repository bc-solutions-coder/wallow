using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Mappings;
using Foundry.Inquiries.Domain.Entities;

namespace Foundry.Inquiries.Application.Queries.GetInquiryComments;

public sealed class GetInquiryCommentsHandler(IInquiryCommentRepository commentRepository)
{
    public async Task<IReadOnlyList<InquiryCommentDto>> Handle(
        GetInquiryCommentsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<InquiryComment> comments = await commentRepository.GetByInquiryIdAsync(
            query.InquiryId,
            query.IncludeInternal,
            cancellationToken);

        return comments.Select(c => c.ToCommentDto()).ToList();
    }
}
