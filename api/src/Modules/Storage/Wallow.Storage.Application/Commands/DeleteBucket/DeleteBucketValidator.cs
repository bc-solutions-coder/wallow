using FluentValidation;

namespace Wallow.Storage.Application.Commands.DeleteBucket;

public sealed class DeleteBucketValidator : AbstractValidator<DeleteBucketCommand>
{
    public DeleteBucketValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Bucket name is required");
    }
}
