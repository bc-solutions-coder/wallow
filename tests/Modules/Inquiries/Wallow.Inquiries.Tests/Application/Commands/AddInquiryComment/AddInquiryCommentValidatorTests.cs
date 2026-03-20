using FluentValidation.TestHelper;
using Wallow.Inquiries.Application.Commands.AddInquiryComment;
using Wallow.Inquiries.Domain.Identity;

namespace Wallow.Inquiries.Tests.Application.Commands.AddInquiryComment;

public class AddInquiryCommentValidatorTests
{
    private readonly AddInquiryCommentValidator _validator = new();

    private static AddInquiryCommentCommand Valid() =>
        new(InquiryId.New(), "user-123", "John Doe", "This is a valid comment.", false, Guid.NewGuid());

    [Fact]
    public void Should_Have_Error_When_Content_Is_Empty()
    {
        AddInquiryCommentCommand command = Valid() with { Content = "" };
        TestValidationResult<AddInquiryCommentCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Should_Have_Error_When_Content_Exceeds_5000_Characters()
    {
        AddInquiryCommentCommand command = Valid() with { Content = new string('x', 5001) };
        TestValidationResult<AddInquiryCommentCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Should_Have_Error_When_AuthorId_Is_Empty()
    {
        AddInquiryCommentCommand command = Valid() with { AuthorId = "" };
        TestValidationResult<AddInquiryCommentCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.AuthorId);
    }

    [Fact]
    public void Should_Have_Error_When_AuthorName_Is_Empty()
    {
        AddInquiryCommentCommand command = Valid() with { AuthorName = "" };
        TestValidationResult<AddInquiryCommentCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.AuthorName);
    }

    [Fact]
    public void Should_Have_Error_When_AuthorName_Exceeds_200_Characters()
    {
        AddInquiryCommentCommand command = Valid() with { AuthorName = new string('x', 201) };
        TestValidationResult<AddInquiryCommentCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.AuthorName);
    }

    [Fact]
    public void Should_Have_Error_When_InquiryId_Is_Empty()
    {
        AddInquiryCommentCommand command = Valid() with { InquiryId = InquiryId.Create(Guid.Empty) };
        TestValidationResult<AddInquiryCommentCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.InquiryId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_All_Fields_Valid()
    {
        AddInquiryCommentCommand command = Valid();
        TestValidationResult<AddInquiryCommentCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
