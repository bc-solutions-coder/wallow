using FluentValidation;

namespace Wallow.Inquiries.Application.Commands.UpdateInquiryStatus;

public sealed class UpdateInquiryStatusValidator : AbstractValidator<UpdateInquiryStatusCommand>
{
    public UpdateInquiryStatusValidator()
    {
        RuleFor(x => x.InquiryId)
            .NotEmpty().WithMessage("Inquiry ID is required");

        RuleFor(x => x.NewStatus)
            .IsInEnum().WithMessage("New status must be a valid inquiry status");
    }
}
