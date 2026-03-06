using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Inquiries.Infrastructure.Persistence.Repositories;

public sealed class InquiryRepository : IInquiryRepository
{
    private readonly InquiriesDbContext _context;

    public InquiryRepository(InquiriesDbContext context)
    {
        _context = context;
    }

    public Task<Inquiry?> GetByIdAsync(InquiryId id, CancellationToken cancellationToken = default)
    {
        return _context.Inquiries
            .AsTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Inquiry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Inquiries
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Inquiry inquiry, CancellationToken cancellationToken = default)
    {
        await _context.Inquiries.AddAsync(inquiry, cancellationToken);
    }

    public Task UpdateAsync(Inquiry inquiry, CancellationToken cancellationToken = default)
    {
        _context.Inquiries.Update(inquiry);
        return Task.CompletedTask;
    }
}
