using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;

namespace Wallow.Inquiries.Application.Interfaces;

public interface IInquiryCommentRepository
{
    Task<InquiryComment?> GetByIdAsync(InquiryCommentId id, CancellationToken cancellationToken = default);
    Task AddAsync(InquiryComment comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InquiryComment>> GetByInquiryIdAsync(InquiryId inquiryId, bool includeInternal, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
