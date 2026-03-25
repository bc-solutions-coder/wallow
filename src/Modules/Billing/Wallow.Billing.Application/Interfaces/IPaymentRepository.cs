using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;

namespace Wallow.Billing.Application.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetAllAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);
    Task<int> CountAllAsync(CancellationToken cancellationToken = default);
    void Add(Payment payment);
    void Update(Payment payment);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
