using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Queries.GetInvoicesByUserId;

public sealed class GetInvoicesByUserIdHandler(IInvoiceRepository invoiceRepository)
{
    public async Task<Result<IReadOnlyList<InvoiceDto>>> Handle(
        GetInvoicesByUserIdQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Invoice> invoices = await invoiceRepository.GetByUserIdAsync(query.UserId, cancellationToken);
        List<InvoiceDto> dtos = invoices.Select(i => i.ToDto()).ToList();
        return Result.Success<IReadOnlyList<InvoiceDto>>(dtos);
    }
}
