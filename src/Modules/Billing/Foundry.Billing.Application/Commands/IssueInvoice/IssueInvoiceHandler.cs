using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Application.Mappings;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Application.Commands.IssueInvoice;

public sealed class IssueInvoiceHandler(
    IInvoiceRepository invoiceRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<InvoiceDto>> Handle(
        IssueInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        InvoiceId invoiceId = InvoiceId.Create(command.InvoiceId);
        Invoice? invoice = await invoiceRepository.GetByIdWithLineItemsAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result.Failure<InvoiceDto>(
                Error.NotFound("Invoice", command.InvoiceId));
        }

        invoice.Issue(command.IssuedByUserId, timeProvider);
        await invoiceRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(invoice.ToDto());
    }
}
