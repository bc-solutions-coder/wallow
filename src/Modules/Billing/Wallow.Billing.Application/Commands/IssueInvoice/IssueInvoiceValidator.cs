using FluentValidation;

namespace Wallow.Billing.Application.Commands.IssueInvoice;

public sealed class IssueInvoiceValidator : AbstractValidator<IssueInvoiceCommand>
{
    public IssueInvoiceValidator()
    {
        RuleFor(x => x.InvoiceId)
            .NotEmpty().WithMessage("Invoice ID is required");

        RuleFor(x => x.IssuedByUserId)
            .NotEmpty().WithMessage("Issued by user ID is required");
    }
}
