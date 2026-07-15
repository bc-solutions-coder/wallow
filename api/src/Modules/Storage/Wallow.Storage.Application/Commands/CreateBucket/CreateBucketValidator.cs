using FluentValidation;

namespace Wallow.Storage.Application.Commands.CreateBucket;

public sealed class CreateBucketValidator : AbstractValidator<CreateBucketCommand>
{
    public CreateBucketValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Bucket name is required")
            .MaximumLength(100).WithMessage("Bucket name must not exceed 100 characters")
            .Matches("^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$")
            .WithMessage("Bucket name must be lowercase alphanumeric with optional hyphens, starting and ending with alphanumeric");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => x.Description is not null);

        RuleFor(x => x.MaxFileSizeBytes)
            .GreaterThanOrEqualTo(0).WithMessage("Max file size cannot be negative");

        RuleFor(x => x.RetentionDays)
            .GreaterThan(0).WithMessage("Retention days must be positive when specified")
            .When(x => x.RetentionDays.HasValue);

        RuleFor(x => x.RetentionAction)
            .NotNull().WithMessage("Retention action is required when retention days are specified")
            .When(x => x.RetentionDays.HasValue);
    }
}
