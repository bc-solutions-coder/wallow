using FluentValidation;

namespace Wallow.Notifications.Application.Channels.Push.Commands.SetTenantPushEnabled;

public sealed class SetTenantPushEnabledValidator : AbstractValidator<SetTenantPushEnabledCommand>
{
    public SetTenantPushEnabledValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");

        RuleFor(x => x.Platform)
            .IsInEnum().WithMessage("Invalid push platform");
    }
}
