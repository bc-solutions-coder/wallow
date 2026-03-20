using FluentValidation;

namespace Wallow.Billing.Application.Commands.ProcessPayment;

public sealed class ProcessPaymentValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentValidator()
    {
        RuleFor(x => x.InvoiceId)
            .NotEmpty().WithMessage("Invoice ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be a 3-letter ISO code");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("Payment method is required");
    }
}
