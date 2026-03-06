using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;

namespace Foundry.Inquiries.Application.Interfaces;

public interface IInquiryRepository
{
    Task<Inquiry?> GetByIdAsync(InquiryId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Inquiry>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Inquiry inquiry, CancellationToken cancellationToken = default);
    Task UpdateAsync(Inquiry inquiry, CancellationToken cancellationToken = default);
}
