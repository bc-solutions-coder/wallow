using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Queries.GetInvoiceById;

public sealed class GetInvoiceByIdHandler(IInvoiceRepository invoiceRepository)
{
    public async Task<Result<InvoiceDto>> Handle(
        GetInvoiceByIdQuery query,
        CancellationToken cancellationToken)
    {
        InvoiceId invoiceId = InvoiceId.Create(query.InvoiceId);
        Invoice? invoice = await invoiceRepository.GetByIdWithLineItemsAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result.Failure<InvoiceDto>(Error.NotFound("Invoice", query.InvoiceId));
        }

        return Result.Success(invoice.ToDto());
    }
}
