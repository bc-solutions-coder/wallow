using FluentValidation.TestHelper;
using Foundry.Inquiries.Application.Commands.SubmitInquiry;

namespace Foundry.Inquiries.Tests.Application.Commands.SubmitInquiry;

public class SubmitInquiryBoundaryTests
{
    private readonly SubmitInquiryValidator _validator = new();

    private static SubmitInquiryCommand Valid() =>
        new("John Doe", "john@example.com", "Acme", "Web Application", "$10k - $50k", "3 months", "We need help building our platform.", "1.2.3.4", null);

    [Fact]
    public void Should_Not_Have_Error_When_Name_Is_Exactly_200_Characters()
    {
        SubmitInquiryCommand command = Valid() with { Name = new string('x', 200) };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Name_Is_201_Characters()
    {
        SubmitInquiryCommand command = Valid() with { Name = new string('x', 201) };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Email_Is_Exactly_254_Characters()
    {
        // 254 total: local@domain format
        string local = new string('a', 243);
        string email = $"{local}@test.co.uk";
        SubmitInquiryCommand command = Valid() with { Email = email };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Not_Have_Error_When_ProjectType_Is_Exactly_100_Characters()
    {
        SubmitInquiryCommand command = Valid() with { ProjectType = new string('x', 100) };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.ProjectType);
    }

    [Fact]
    public void Should_Have_Error_When_ProjectType_Exceeds_100_Characters()
    {
        SubmitInquiryCommand command = Valid() with { ProjectType = new string('x', 101) };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ProjectType);
    }

    [Fact]
    public void Should_Not_Have_Error_When_BudgetRange_Is_Exactly_100_Characters()
    {
        SubmitInquiryCommand command = Valid() with { BudgetRange = new string('x', 100) };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.BudgetRange);
    }

    [Fact]
    public void Should_Have_Error_When_BudgetRange_Exceeds_100_Characters()
    {
        SubmitInquiryCommand command = Valid() with { BudgetRange = new string('x', 101) };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.BudgetRange);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Timeline_Is_Exactly_100_Characters()
    {
        SubmitInquiryCommand command = Valid() with { Timeline = new string('x', 100) };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Timeline);
    }

    [Fact]
    public void Should_Have_Error_When_Timeline_Exceeds_100_Characters()
    {
        SubmitInquiryCommand command = Valid() with { Timeline = new string('x', 101) };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Timeline);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Message_Is_Exactly_5000_Characters()
    {
        SubmitInquiryCommand command = Valid() with { Message = new string('x', 5000) };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void Should_Have_Error_When_Name_Is_Whitespace()
    {
        SubmitInquiryCommand command = Valid() with { Name = "   " };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Whitespace()
    {
        SubmitInquiryCommand command = Valid() with { Email = "   " };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_Have_Error_When_Message_Is_Whitespace()
    {
        SubmitInquiryCommand command = Valid() with { Message = "   " };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Company_Is_Null()
    {
        SubmitInquiryCommand command = Valid() with { Company = null };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_HoneypotField_Is_Null()
    {
        SubmitInquiryCommand command = Valid() with { HoneypotField = null };
        TestValidationResult<SubmitInquiryCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
