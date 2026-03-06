using FluentValidation;

namespace Foundry.Storage.Application.Queries.GetUploadPresignedUrl;

public sealed class GetUploadPresignedUrlValidator : AbstractValidator<GetUploadPresignedUrlQuery>
{
    public GetUploadPresignedUrlValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.BucketName)
            .NotEmpty().WithMessage("Bucket name is required")
            .MaximumLength(100).WithMessage("Bucket name must not exceed 100 characters");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required")
            .MaximumLength(500).WithMessage("File name must not exceed 500 characters")
            .Must(name => !ContainsPathTraversal(name))
            .WithMessage("File name must not contain path traversal sequences")
            .When(x => !string.IsNullOrEmpty(x.FileName));

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required")
            .MaximumLength(200).WithMessage("Content type must not exceed 200 characters");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("File size must be greater than 0");

        RuleFor(x => x.Path)
            .MaximumLength(500).WithMessage("Path must not exceed 500 characters")
            .Must(path => !ContainsPathTraversal(path!))
            .WithMessage("Path must not contain path traversal sequences")
            .When(x => x.Path is not null);
    }

    private static bool ContainsPathTraversal(string value)
    {
        return value.Contains("..", StringComparison.Ordinal) ||
               value.StartsWith('/') ||
               value.StartsWith('\\') ||
               (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':');
    }
}
