using FluentValidation;

namespace Wallow.Notifications.Application.Channels.Push.Commands.UpsertTenantPushConfig;

public sealed class UpsertTenantPushConfigValidator : AbstractValidator<UpsertTenantPushConfigCommand>
{
    public UpsertTenantPushConfigValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");

        RuleFor(x => x.Platform)
            .IsInEnum().WithMessage("Invalid push platform");

        RuleFor(x => x.RawCredentials)
            .NotEmpty().WithMessage("Credentials are required");
    }
}
