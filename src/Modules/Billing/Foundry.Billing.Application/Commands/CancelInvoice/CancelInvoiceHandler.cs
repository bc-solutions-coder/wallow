using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Application.Mappings;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Application.Commands.CancelInvoice;

public sealed class CancelInvoiceHandler(
    IInvoiceRepository invoiceRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<InvoiceDto>> Handle(
        CancelInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        InvoiceId invoiceId = InvoiceId.Create(command.InvoiceId);
        Invoice? invoice = await invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result.Failure<InvoiceDto>(
                Error.NotFound("Invoice", command.InvoiceId));
        }

        invoice.Cancel(command.CancelledByUserId, timeProvider);
        await invoiceRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(invoice.ToDto());
    }
}
