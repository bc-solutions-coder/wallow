using FluentValidation;

namespace Wallow.Billing.Application.Commands.AddLineItem;

public sealed class AddLineItemValidator : AbstractValidator<AddLineItemCommand>
{
    public AddLineItemValidator()
    {
        RuleFor(x => x.InvoiceId)
            .NotEmpty().WithMessage("Invoice ID is required");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Unit price must be greater than or equal to zero");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero");

        RuleFor(x => x.UpdatedByUserId)
            .NotEmpty().WithMessage("Updated by user ID is required");
    }
}
