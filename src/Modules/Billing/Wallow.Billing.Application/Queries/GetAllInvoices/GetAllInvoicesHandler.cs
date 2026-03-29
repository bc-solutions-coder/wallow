using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Queries.GetAllInvoices;

public sealed class GetAllInvoicesHandler(IInvoiceRepository invoiceRepository)
{
    public async Task<Result<PagedResult<InvoiceDto>>> Handle(
        GetAllInvoicesQuery query,
        CancellationToken cancellationToken)
    {
        int totalCount = await invoiceRepository.CountAllAsync(cancellationToken);
        IReadOnlyList<Invoice> invoices = await invoiceRepository.GetAllAsync(query.Skip, query.Take, cancellationToken);
        List<InvoiceDto> dtos = invoices.Select(i => i.ToDto()).ToList();
        int page = (query.Skip / query.Take) + 1;
        return Result.Success(new PagedResult<InvoiceDto>(dtos, totalCount, page, query.Take));
    }
}
