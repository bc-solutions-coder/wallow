using FluentValidation.TestHelper;
using Wallow.Storage.Application.Commands.DeleteFile;

namespace Wallow.Storage.Tests.Application;

public class DeleteFileValidatorTests
{
    private readonly DeleteFileValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        DeleteFileCommand command = new(Guid.NewGuid(), Guid.NewGuid());

        TestValidationResult<DeleteFileCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_TenantIdIsEmpty()
    {
        DeleteFileCommand command = new(Guid.Empty, Guid.NewGuid());

        TestValidationResult<DeleteFileCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void Should_Have_Error_When_FileIdIsEmpty()
    {
        DeleteFileCommand command = new(Guid.NewGuid(), Guid.Empty);

        TestValidationResult<DeleteFileCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FileId);
    }

    [Fact]
    public void Should_Have_Errors_When_BothIdsAreEmpty()
    {
        DeleteFileCommand command = new(Guid.Empty, Guid.Empty);

        TestValidationResult<DeleteFileCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TenantId);
        result.ShouldHaveValidationErrorFor(x => x.FileId);
    }
}
