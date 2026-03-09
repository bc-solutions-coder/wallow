using FluentValidation.TestHelper;
using Foundry.Storage.Application.Commands.CreateBucket;
using Foundry.Storage.Domain.Enums;

namespace Foundry.Storage.Tests.Application;

public class CreateBucketValidatorTests
{
    private readonly CreateBucketValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        CreateBucketCommand command = new("my-bucket", "A description");

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Should_Have_Error_When_NameIsEmpty(string? name)
    {
        CreateBucketCommand command = new(name!);

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_NameExceeds100Characters()
    {
        string longName = new('a', 101);
        CreateBucketCommand command = new(longName);

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("MyBucket")]
    [InlineData("my_bucket")]
    [InlineData("-my-bucket")]
    [InlineData("my-bucket-")]
    [InlineData("my bucket")]
    public void Should_Have_Error_When_NameHasInvalidFormat(string name)
    {
        CreateBucketCommand command = new(name);

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("my-bucket")]
    [InlineData("bucket-123")]
    [InlineData("1")]
    [InlineData("a1b2c3")]
    public void Should_Not_Have_Error_When_NameHasValidFormat(string name)
    {
        CreateBucketCommand command = new(name);

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_DescriptionExceeds500Characters()
    {
        string longDescription = new('x', 501);
        CreateBucketCommand command = new("valid", Description: longDescription);

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Should_Not_Have_Error_When_DescriptionIsNull()
    {
        CreateBucketCommand command = new("valid");

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Should_Have_Error_When_MaxFileSizeBytesIsNegative()
    {
        CreateBucketCommand command = new("valid", MaxFileSizeBytes: -1);

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MaxFileSizeBytes);
    }

    [Fact]
    public void Should_Not_Have_Error_When_MaxFileSizeBytesIsZero()
    {
        CreateBucketCommand command = new("valid", MaxFileSizeBytes: 0);

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.MaxFileSizeBytes);
    }

    [Fact]
    public void Should_Have_Error_When_RetentionDaysIsZero()
    {
        CreateBucketCommand command = new("valid", RetentionDays: 0, RetentionAction: RetentionAction.Delete);

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.RetentionDays);
    }

    [Fact]
    public void Should_Have_Error_When_RetentionDaysIsNegative()
    {
        CreateBucketCommand command = new("valid", RetentionDays: -5, RetentionAction: RetentionAction.Delete);

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.RetentionDays);
    }

    [Fact]
    public void Should_Have_Error_When_RetentionDaysSetWithoutRetentionAction()
    {
        CreateBucketCommand command = new("valid", RetentionDays: 30);

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.RetentionAction);
    }

    [Fact]
    public void Should_Not_Have_Error_When_RetentionDaysAndActionBothSet()
    {
        CreateBucketCommand command = new("valid", RetentionDays: 30, RetentionAction: RetentionAction.Archive);

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.RetentionDays);
        result.ShouldNotHaveValidationErrorFor(x => x.RetentionAction);
    }

    [Fact]
    public void Should_Not_Have_Error_When_NoRetentionSpecified()
    {
        CreateBucketCommand command = new("valid");

        TestValidationResult<CreateBucketCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.RetentionDays);
        result.ShouldNotHaveValidationErrorFor(x => x.RetentionAction);
    }
}
