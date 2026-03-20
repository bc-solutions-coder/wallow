using FluentValidation;

namespace Wallow.Storage.Application.Commands.DeleteFile;

public sealed class DeleteFileValidator : AbstractValidator<DeleteFileCommand>
{
    public DeleteFileValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");

        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File ID is required");
    }
}
