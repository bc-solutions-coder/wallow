using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Inquiries.Infrastructure.Persistence.Repositories;

public sealed class InquiryCommentRepository(InquiriesDbContext context) : IInquiryCommentRepository
{
    public Task<InquiryComment?> GetByIdAsync(InquiryCommentId id, CancellationToken cancellationToken = default)
    {
        return context.InquiryComments
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AddAsync(InquiryComment comment, CancellationToken cancellationToken = default)
    {
        await context.InquiryComments.AddAsync(comment, cancellationToken);
    }

    public async Task<IReadOnlyList<InquiryComment>> GetByInquiryIdAsync(
        InquiryId inquiryId,
        bool includeInternal,
        CancellationToken cancellationToken = default)
    {
        IQueryable<InquiryComment> query = context.InquiryComments
            .Where(c => c.InquiryId == inquiryId);

        if (!includeInternal)
        {
            query = query.Where(c => !c.IsInternal);
        }

        return await query
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
