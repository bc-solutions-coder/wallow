using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Commands.IssueInvoice;

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
