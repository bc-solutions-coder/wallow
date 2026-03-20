using FluentValidation;

namespace Wallow.Billing.Application.Commands.CancelInvoice;

public sealed class CancelInvoiceValidator : AbstractValidator<CancelInvoiceCommand>
{
    public CancelInvoiceValidator()
    {
        RuleFor(x => x.InvoiceId)
            .NotEmpty().WithMessage("Invoice ID is required");

        RuleFor(x => x.CancelledByUserId)
            .NotEmpty().WithMessage("Cancelled by user ID is required");
    }
}
