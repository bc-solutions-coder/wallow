using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Inquiries.Infrastructure.Persistence.Repositories;

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
}
