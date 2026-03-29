using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;

namespace Wallow.Inquiries.Application.Interfaces;

public interface IInquiryRepository
{
    Task<Inquiry?> GetByIdAsync(InquiryId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Inquiry>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Inquiry inquiry, CancellationToken cancellationToken = default);
    Task UpdateAsync(Inquiry inquiry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Inquiry>> GetBySubmitterAsync(string submitterId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Inquiry>> GetUnlinkedByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
