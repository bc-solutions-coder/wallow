using System.Diagnostics;
using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Application.Telemetry;
using Wallow.Billing.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Commands.CreateInvoice;

public sealed class CreateInvoiceHandler(
    IInvoiceRepository invoiceRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<InvoiceDto>> Handle(
        CreateInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        using Activity? activity = BillingModuleTelemetry.ActivitySource.StartActivity("Billing.CreateInvoice");

        try
        {
            bool exists = await invoiceRepository.ExistsByInvoiceNumberAsync(
                command.InvoiceNumber,
                cancellationToken);

            if (exists)
            {
                return Result.Failure<InvoiceDto>(
                    Error.Conflict($"Invoice '{command.InvoiceNumber}' already exists"));
            }

            Invoice invoice = Invoice.Create(
                command.UserId,
                command.InvoiceNumber,
                command.Currency,
                command.UserId,
                timeProvider,
                command.DueDate,
                command.CustomFields);

            invoiceRepository.Add(invoice);
            await invoiceRepository.SaveChangesAsync(cancellationToken);

            activity?.SetTag("invoice.id", invoice.Id.Value.ToString());
            activity?.SetTag("invoice.number", invoice.InvoiceNumber);
            activity?.SetTag("invoice.currency", command.Currency);

            return Result.Success(invoice.ToDto());
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception",
                tags: new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message }
                }));
            throw;
        }
    }
}
