using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Foundry.Shared.Kernel.Pagination;

namespace Foundry.Billing.Application.Interfaces;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByIdWithLineItemsAsync(InvoiceId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PagedResult<Invoice>> GetByUserIdPagedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<Invoice>> GetAllPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);
    void Add(Invoice invoice);
    void Update(Invoice invoice);
    void Remove(Invoice invoice);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
