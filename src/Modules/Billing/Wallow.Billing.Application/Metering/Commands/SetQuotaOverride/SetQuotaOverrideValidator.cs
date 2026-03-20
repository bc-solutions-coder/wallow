using FluentValidation;

namespace Wallow.Billing.Application.Metering.Commands.SetQuotaOverride;

public sealed class SetQuotaOverrideValidator : AbstractValidator<SetQuotaOverrideCommand>
{
    public SetQuotaOverrideValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");

        RuleFor(x => x.MeterCode)
            .NotEmpty().WithMessage("Meter code is required")
            .MaximumLength(100).WithMessage("Meter code must not exceed 100 characters");

        RuleFor(x => x.Limit)
            .GreaterThanOrEqualTo(0).WithMessage("Limit must be non-negative");

        RuleFor(x => x.Period)
            .IsInEnum().WithMessage("Invalid quota period");

        RuleFor(x => x.OnExceeded)
            .IsInEnum().WithMessage("Invalid quota action");
    }
}
