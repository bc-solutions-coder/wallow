using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Application.Mappings;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Application.Commands.AddLineItem;

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
