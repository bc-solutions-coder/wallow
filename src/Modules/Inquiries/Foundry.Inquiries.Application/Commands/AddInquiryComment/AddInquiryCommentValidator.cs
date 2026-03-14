using FluentValidation;

namespace Foundry.Inquiries.Application.Commands.AddInquiryComment;

public sealed class AddInquiryCommentValidator : AbstractValidator<AddInquiryCommentCommand>
{
    public AddInquiryCommentValidator()
    {
        RuleFor(x => x.InquiryId)
            .Must(id => id.Value != Guid.Empty).WithMessage("Inquiry ID is required");

        RuleFor(x => x.AuthorId)
            .NotEmpty().WithMessage("Author ID is required");

        RuleFor(x => x.AuthorName)
            .NotEmpty().WithMessage("Author name is required")
            .MaximumLength(200).WithMessage("Author name must not exceed 200 characters");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required")
            .MaximumLength(5000).WithMessage("Content must not exceed 5000 characters");
    }
}
