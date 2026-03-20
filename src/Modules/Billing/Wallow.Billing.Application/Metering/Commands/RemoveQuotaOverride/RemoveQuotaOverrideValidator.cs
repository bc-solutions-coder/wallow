using FluentValidation;

namespace Wallow.Billing.Application.Metering.Commands.RemoveQuotaOverride;

public sealed class RemoveQuotaOverrideValidator : AbstractValidator<RemoveQuotaOverrideCommand>
{
    public RemoveQuotaOverrideValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");

        RuleFor(x => x.MeterCode)
            .NotEmpty().WithMessage("Meter code is required")
            .MaximumLength(100).WithMessage("Meter code must not exceed 100 characters");
    }
}
