using Microsoft.EntityFrameworkCore;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;

namespace Wallow.Inquiries.Infrastructure.Persistence.Repositories;

public sealed class InquiryRepository(InquiriesDbContext context) : IInquiryRepository
{

    public Task<Inquiry?> GetByIdAsync(InquiryId id, CancellationToken cancellationToken = default)
    {
        return context.Inquiries
            .AsTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Inquiry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Inquiries
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Inquiry inquiry, CancellationToken cancellationToken = default)
    {
        await context.Inquiries.AddAsync(inquiry, cancellationToken);
    }

    public Task UpdateAsync(Inquiry inquiry, CancellationToken cancellationToken = default)
    {
        context.Inquiries.Update(inquiry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Inquiry>> GetBySubmitterAsync(string submitterId, CancellationToken cancellationToken = default)
    {
        return await context.Inquiries
            .Where(i => i.SubmitterId == submitterId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Inquiry>> GetUnlinkedByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Inquiries
            .AsTracking()
            .Where(i => i.Email == email && i.SubmitterId == null)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
