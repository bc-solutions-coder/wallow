using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Mappings;
using Wallow.Inquiries.Domain.Entities;

namespace Wallow.Inquiries.Application.Queries.GetInquiryComments;

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
