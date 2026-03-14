using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;

namespace Foundry.Inquiries.Application.Interfaces;

public interface IInquiryCommentRepository
{
    Task<InquiryComment?> GetByIdAsync(InquiryCommentId id, CancellationToken cancellationToken = default);
    Task AddAsync(InquiryComment comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InquiryComment>> GetByInquiryIdAsync(InquiryId inquiryId, bool includeInternal, CancellationToken cancellationToken = default);
}
