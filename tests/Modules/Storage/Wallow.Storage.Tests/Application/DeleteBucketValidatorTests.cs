using FluentValidation.TestHelper;
using Wallow.Storage.Application.Commands.DeleteBucket;

namespace Wallow.Storage.Tests.Application;

public class DeleteBucketValidatorTests
{
    private readonly DeleteBucketValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_NameIsProvided()
    {
        DeleteBucketCommand command = new(Guid.NewGuid(), "my-bucket");

        TestValidationResult<DeleteBucketCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Should_Have_Error_When_NameIsEmpty(string? name)
    {
        DeleteBucketCommand command = new(Guid.NewGuid(), name!);

        TestValidationResult<DeleteBucketCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Not_Have_Error_When_ForceIsFalse()
    {
        DeleteBucketCommand command = new(Guid.NewGuid(), "bucket", Force: false);

        TestValidationResult<DeleteBucketCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_ForceIsTrue()
    {
        DeleteBucketCommand command = new(Guid.NewGuid(), "bucket", Force: true);

        TestValidationResult<DeleteBucketCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
