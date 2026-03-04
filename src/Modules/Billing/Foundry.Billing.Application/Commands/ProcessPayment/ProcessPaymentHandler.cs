using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Application.Mappings;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Enums;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Application.Commands.ProcessPayment;

public sealed class ProcessPaymentHandler(
    IPaymentRepository paymentRepository,
    IInvoiceRepository invoiceRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<PaymentDto>> Handle(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        InvoiceId invoiceId = InvoiceId.Create(command.InvoiceId);
        Invoice? invoice = await invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result.Failure<PaymentDto>(
                Error.NotFound("Invoice", command.InvoiceId));
        }

        if (command.Amount > invoice.TotalAmount.Amount)
        {
            return Result.Failure<PaymentDto>(
                Error.Validation("Payment.Overpayment", "Payment amount exceeds outstanding balance."));
        }

        if (!string.Equals(command.Currency, invoice.TotalAmount.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<PaymentDto>(
                Error.Validation("Payment.CurrencyMismatch", "Payment currency does not match invoice currency."));
        }

        if (!Enum.TryParse<PaymentMethod>(command.PaymentMethod, ignoreCase: true, out PaymentMethod method))
        {
            return Result.Failure<PaymentDto>(
                Error.Validation($"Invalid payment method '{command.PaymentMethod}'. Valid values: {string.Join(", ", Enum.GetNames<PaymentMethod>())}"));
        }

        Money amount = Money.Create(command.Amount, command.Currency);
        Payment payment = Payment.Create(
            invoiceId,
            command.UserId,
            amount,
            method,
            command.UserId,
            timeProvider,
            command.CustomFields);

        paymentRepository.Add(payment);

        if (invoice.Status is InvoiceStatus.Issued or InvoiceStatus.Overdue)
        {
            IReadOnlyList<Payment> existingPayments = await paymentRepository.GetByInvoiceIdAsync(invoiceId, cancellationToken);
            Money totalPaid = existingPayments
                .Where(p => p.Status == PaymentStatus.Completed)
                .Aggregate(Money.Zero(invoice.TotalAmount.Currency), (sum, p) => sum + p.Amount);
            totalPaid = totalPaid + amount;

            if (totalPaid.Amount >= invoice.TotalAmount.Amount)
            {
                invoice.MarkAsPaid(payment.Id.Value, command.UserId, timeProvider);
            }
        }

        await paymentRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(payment.ToDto());
    }
}
