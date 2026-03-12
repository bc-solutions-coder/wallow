using FluentValidation.TestHelper;
using Foundry.Inquiries.Application.Commands.SubmitInquiry;

namespace Foundry.Inquiries.Tests.Application.Commands.SubmitInquiry;

public class SubmitInquiryValidatorTests
{
    private readonly SubmitInquiryValidator _validator = new();

    private static SubmitInquiryCommand Valid() =>
        new("John Doe", "john@example.com", "Acme", "Web Application", "$10k - $50k", "3 months", "We need help building our platform.", "1.2.3.4", null);

    [Fact]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        SubmitInquiryCommand command = Valid() with { Name = "" };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Name_Exceeds_200_Characters()
    {
        SubmitInquiryCommand command = Valid() with { Name = new string('x', 201) };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        SubmitInquiryCommand command = Valid() with { Email = "" };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Invalid()
    {
        SubmitInquiryCommand command = Valid() with { Email = "not-an-email" };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Exceeds_254_Characters()
    {
        // Create a valid-format email that exceeds 254 characters
        string longLocal = new string('a', 246);
        string longEmail = $"{longLocal}@test.com";
        SubmitInquiryCommand command = Valid() with { Email = longEmail };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must not exceed 254 characters");
    }

    [Fact]
    public void Should_Have_Error_When_ProjectType_Is_Empty()
    {
        SubmitInquiryCommand command = Valid() with { ProjectType = "" };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ProjectType);
    }

    [Fact]
    public void Should_Have_Error_When_BudgetRange_Is_Empty()
    {
        SubmitInquiryCommand command = Valid() with { BudgetRange = "" };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.BudgetRange);
    }

    [Fact]
    public void Should_Have_Error_When_Timeline_Is_Empty()
    {
        SubmitInquiryCommand command = Valid() with { Timeline = "" };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Timeline);
    }

    [Fact]
    public void Should_Have_Error_When_Message_Is_Empty()
    {
        SubmitInquiryCommand command = Valid() with { Message = "" };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void Should_Have_Error_When_Message_Exceeds_5000_Characters()
    {
        SubmitInquiryCommand command = Valid() with { Message = new string('x', 5001) };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void Should_Have_Error_When_SubmitterIpAddress_Is_Empty()
    {
        SubmitInquiryCommand command = Valid() with { SubmitterIpAddress = "" };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SubmitterIpAddress);
    }

    [Fact]
    public void Should_Not_Have_Error_When_All_Fields_Valid()
    {
        SubmitInquiryCommand command = Valid();
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
