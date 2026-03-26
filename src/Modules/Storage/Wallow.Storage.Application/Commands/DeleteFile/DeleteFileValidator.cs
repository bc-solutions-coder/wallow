using FluentValidation;

namespace Wallow.Storage.Application.Commands.DeleteFile;

public sealed class DeleteFileValidator : AbstractValidator<DeleteFileCommand>
{
    public DeleteFileValidator()
    {
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File ID is required");
    }
}
