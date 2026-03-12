using FluentValidation.TestHelper;
using Foundry.Messaging.Application.Conversations.Commands.CreateConversation;

namespace Foundry.Messaging.Tests.Application.Commands.CreateConversation;

public class CreateConversationValidatorTests
{
    private readonly CreateConversationValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_InitiatorId_Is_Empty()
    {
        CreateConversationCommand command = new(Guid.Empty, null, null, "Direct", null);
        TestValidationResult<CreateConversationCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.InitiatorId);
    }

    [Fact]
    public void Should_Have_Error_When_Type_Is_Empty()
    {
        CreateConversationCommand command = new(Guid.NewGuid(), null, null, "", null);
        TestValidationResult<CreateConversationCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void Should_Have_Error_When_Type_Is_Invalid()
    {
        CreateConversationCommand command = new(Guid.NewGuid(), null, null, "Invalid", null);
        TestValidationResult<CreateConversationCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void Should_Have_Error_When_Direct_And_RecipientId_Is_Empty()
    {
        CreateConversationCommand command = new(Guid.NewGuid(), null, null, "Direct", null);
        TestValidationResult<CreateConversationCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RecipientId);
    }

    [Fact]
    public void Should_Have_Error_When_Group_And_MemberIds_Is_Empty()
    {
        CreateConversationCommand command = new(Guid.NewGuid(), null, null, "Group", "Test Group");
        TestValidationResult<CreateConversationCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.MemberIds);
    }

    [Fact]
    public void Should_Have_Error_When_Group_And_Name_Is_Empty()
    {
        CreateConversationCommand command = new(Guid.NewGuid(), null, [Guid.NewGuid()], "Group", null);
        TestValidationResult<CreateConversationCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Direct_With_Valid_Data()
    {
        CreateConversationCommand command = new(Guid.NewGuid(), Guid.NewGuid(), null, "Direct", null);
        TestValidationResult<CreateConversationCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_Group_With_Valid_Data()
    {
        CreateConversationCommand command = new(Guid.NewGuid(), null, [Guid.NewGuid(), Guid.NewGuid()], "Group", "Test Group");
        TestValidationResult<CreateConversationCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
