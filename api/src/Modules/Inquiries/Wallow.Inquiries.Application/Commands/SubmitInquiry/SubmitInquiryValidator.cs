using FluentValidation;

namespace Wallow.Inquiries.Application.Commands.SubmitInquiry;

public sealed class SubmitInquiryValidator : AbstractValidator<SubmitInquiryCommand>
{
    public SubmitInquiryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("A valid email address is required")
            .MaximumLength(254).WithMessage("Email must not exceed 254 characters");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone is required")
            .MaximumLength(100).WithMessage("Phone must not exceed 100 characters");

        RuleFor(x => x.ProjectType)
            .NotEmpty().WithMessage("Project type is required")
            .MaximumLength(100).WithMessage("Project type must not exceed 100 characters");

        RuleFor(x => x.BudgetRange)
            .NotEmpty().WithMessage("Budget range is required")
            .MaximumLength(100).WithMessage("Budget range must not exceed 100 characters");

        RuleFor(x => x.Timeline)
            .NotEmpty().WithMessage("Timeline is required")
            .MaximumLength(100).WithMessage("Timeline must not exceed 100 characters");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(5000).WithMessage("Message must not exceed 5000 characters");
    }
}
