using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Commands.AddLineItem;

public sealed class AddLineItemHandler(
    IInvoiceRepository invoiceRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<InvoiceDto>> Handle(
        AddLineItemCommand command,
        CancellationToken cancellationToken)
    {
        InvoiceId invoiceId = InvoiceId.Create(command.InvoiceId);
        Invoice? invoice = await invoiceRepository.GetByIdWithLineItemsAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result.Failure<InvoiceDto>(
                Error.NotFound("Invoice", command.InvoiceId));
        }

        Money unitPrice = Money.Create(command.UnitPrice, invoice.TotalAmount.Currency);
        invoice.AddLineItem(
            command.Description,
            unitPrice,
            command.Quantity,
            command.UpdatedByUserId,
            timeProvider);

        await invoiceRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(invoice.ToDto());
    }
}
