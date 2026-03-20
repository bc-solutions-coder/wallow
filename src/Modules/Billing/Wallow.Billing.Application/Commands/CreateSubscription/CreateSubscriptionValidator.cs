using FluentValidation;

namespace Wallow.Billing.Application.Commands.CreateSubscription;

public sealed class CreateSubscriptionValidator : AbstractValidator<CreateSubscriptionCommand>
{
    public CreateSubscriptionValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.PlanName)
            .NotEmpty().WithMessage("Plan name is required")
            .MaximumLength(100).WithMessage("Plan name must not exceed 100 characters");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to zero");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be a 3-letter ISO code");

        RuleFor(x => x.PeriodEnd)
            .GreaterThan(x => x.StartDate).WithMessage("Period end must be after start date");
    }
}
