using FluentValidation;

namespace Foundry.Configuration.Application.FeatureFlags.Commands.CreateOverride;

public sealed class CreateOverrideValidator : AbstractValidator<CreateOverrideCommand>
{
    public CreateOverrideValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.FlagId)
            .NotEmpty().WithMessage("Feature flag ID is required");

        RuleFor(x => x)
            .Must(cmd => cmd.TenantId.HasValue || cmd.UserId.HasValue)
            .WithMessage("At least one of TenantId or UserId must be provided");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(timeProvider.GetUtcNow().UtcDateTime).WithMessage("ExpiresAt must be in the future")
            .When(x => x.ExpiresAt is not null);
    }
}
