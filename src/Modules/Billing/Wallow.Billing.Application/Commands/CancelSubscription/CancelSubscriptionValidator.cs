using FluentValidation;

namespace Wallow.Billing.Application.Commands.CancelSubscription;

public sealed class CancelSubscriptionValidator : AbstractValidator<CancelSubscriptionCommand>
{
    public CancelSubscriptionValidator()
    {
        RuleFor(x => x.SubscriptionId)
            .NotEmpty().WithMessage("Subscription ID is required");

        RuleFor(x => x.CancelledByUserId)
            .NotEmpty().WithMessage("Cancelled by user ID is required");
    }
}
