using FluentValidation;

namespace Wallow.Notifications.Application.Channels.Push.Commands.RegisterDevice;

public sealed class RegisterDeviceValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().MaximumLength(2048).When(x => x.Platform != Domain.Channels.Push.Enums.PushPlatform.WebPush);
        RuleFor(x => x.Token).Null().When(x => x.Platform == Domain.Channels.Push.Enums.PushPlatform.WebPush);
        RuleFor(x => x.Subscription).NotNull().When(x => x.Platform == Domain.Channels.Push.Enums.PushPlatform.WebPush);
        RuleFor(x => x.SigningKeyId).NotEmpty().MaximumLength(100).When(x => x.Platform == Domain.Channels.Push.Enums.PushPlatform.WebPush);
        RuleFor(x => x.Subscription).Null().When(x => x.Platform != Domain.Channels.Push.Enums.PushPlatform.WebPush);
        RuleFor(x => x.SigningKeyId).Null().When(x => x.Platform != Domain.Channels.Push.Enums.PushPlatform.WebPush);

        RuleFor(x => x.Platform)
            .IsInEnum().WithMessage("Invalid push platform");
    }
}
