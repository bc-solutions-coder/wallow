using System.Diagnostics;
using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Application.Mappings;
using Foundry.Billing.Application.Telemetry;
using Foundry.Billing.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Application.Commands.CreateInvoice;

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
