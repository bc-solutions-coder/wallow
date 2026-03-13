using FluentValidation.TestHelper;
using Foundry.Notifications.Application.Channels.Push.Commands.SendPush;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Tests.Application.Commands.Push;

public class SendPushValidatorTests
{
    private readonly SendPushValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        SendPushCommand command = new(
            new UserId(Guid.NewGuid()),
            TenantId.New(),
            "Title",
            "Body",
            "Alert");

        TestValidationResult<SendPushCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_TitleIsEmpty()
    {
        SendPushCommand command = new(
            new UserId(Guid.NewGuid()),
            TenantId.New(),
            string.Empty,
            "Body",
            "Alert");

        TestValidationResult<SendPushCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Should_Have_Error_When_BodyIsEmpty()
    {
        SendPushCommand command = new(
            new UserId(Guid.NewGuid()),
            TenantId.New(),
            "Title",
            string.Empty,
            "Alert");

        TestValidationResult<SendPushCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Body);
    }

    [Fact]
    public void Should_Have_Error_When_NotificationTypeIsEmpty()
    {
        SendPushCommand command = new(
            new UserId(Guid.NewGuid()),
            TenantId.New(),
            "Title",
            "Body",
            string.Empty);

        TestValidationResult<SendPushCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.NotificationType);
    }
}
