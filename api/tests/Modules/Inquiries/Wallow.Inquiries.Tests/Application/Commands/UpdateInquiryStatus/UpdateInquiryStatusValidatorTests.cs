using FluentValidation.TestHelper;
using Wallow.Inquiries.Application.Commands.UpdateInquiryStatus;
using Wallow.Inquiries.Domain.Enums;

namespace Wallow.Inquiries.Tests.Application.Commands.UpdateInquiryStatus;

public class UpdateInquiryStatusValidatorTests
{
    private readonly UpdateInquiryStatusValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_InquiryId_Is_Empty()
    {
        UpdateInquiryStatusCommand command = new(Guid.Empty, InquiryStatus.Reviewed);
        TestValidationResult<UpdateInquiryStatusCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.InquiryId);
    }

    [Fact]
    public void Should_Have_Error_When_NewStatus_Is_Invalid()
    {
        UpdateInquiryStatusCommand command = new(Guid.NewGuid(), (InquiryStatus)999);
        TestValidationResult<UpdateInquiryStatusCommand> result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.NewStatus);
    }

    [Fact]
    public void Should_Not_Have_Error_When_All_Fields_Valid()
    {
        UpdateInquiryStatusCommand command = new(Guid.NewGuid(), InquiryStatus.Reviewed);
        TestValidationResult<UpdateInquiryStatusCommand> result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
