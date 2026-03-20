using FluentValidation.TestHelper;
using Wallow.Messaging.Application.Conversations.Commands.SendMessage;

namespace Wallow.Messaging.Tests.Application.Commands.SendMessage;

public class SendMessageValidatorTests
{
    private readonly SendMessageValidator _validator = new();

    private static SendMessageCommand Valid() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Hello, world!");

    [Fact]
    public void Should_Have_Error_When_ConversationId_Is_Empty()
    {
        SendMessageCommand command = Valid() with { ConversationId = Guid.Empty };
        TestValidationResult<SendMessageCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ConversationId);
    }

    [Fact]
    public void Should_Have_Error_When_SenderId_Is_Empty()
    {
        SendMessageCommand command = Valid() with { SenderId = Guid.Empty };
        TestValidationResult<SendMessageCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SenderId);
    }

    [Fact]
    public void Should_Have_Error_When_Body_Is_Empty()
    {
        SendMessageCommand command = Valid() with { Body = "" };
        TestValidationResult<SendMessageCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Body);
    }

    [Fact]
    public void Should_Have_Error_When_Body_Exceeds_4000_Characters()
    {
        SendMessageCommand command = Valid() with { Body = new string('x', 4001) };
        TestValidationResult<SendMessageCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Body);
    }

    [Fact]
    public void Should_Not_Have_Error_When_All_Fields_Valid()
    {
        SendMessageCommand command = Valid();
        TestValidationResult<SendMessageCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
