using FluentValidation.TestHelper;
using Foundry.Shared.Contracts.Storage.Commands;
using Foundry.Storage.Application.Commands.UploadFile;

namespace Foundry.Storage.Tests.Application;

public class UploadFileValidatorTests
{
    private readonly UploadFileValidator _validator = new();

    [Fact]
    public async Task Should_Not_Have_Error_When_ValidCommand()
    {
        UploadFileCommand command = CreateValidCommand();

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveValidationErrorFor(x => x.TenantId);
        result.ShouldNotHaveValidationErrorFor(x => x.UserId);
        result.ShouldNotHaveValidationErrorFor(x => x.BucketName);
        result.ShouldNotHaveValidationErrorFor(x => x.FileName);
        result.ShouldNotHaveValidationErrorFor(x => x.ContentType);
        result.ShouldNotHaveValidationErrorFor(x => x.SizeBytes);
        result.ShouldNotHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public async Task Should_Have_Error_When_TenantIdIsEmpty()
    {
        UploadFileCommand command = CreateValidCommand(tenantId: Guid.Empty);

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public async Task Should_Have_Error_When_UserIdIsEmpty()
    {
        UploadFileCommand command = CreateValidCommand(userId: Guid.Empty);

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Have_Error_When_BucketNameIsEmpty(string? bucketName)
    {
        UploadFileCommand command = CreateValidCommand(bucketName: bucketName!);

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.BucketName);
    }

    [Fact]
    public async Task Should_Have_Error_When_BucketNameExceeds100Characters()
    {
        string longName = new('a', 101);
        UploadFileCommand command = CreateValidCommand(bucketName: longName);

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.BucketName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Have_Error_When_FileNameIsEmpty(string? fileName)
    {
        UploadFileCommand command = CreateValidCommand(fileName: fileName!);

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.FileName);
    }

    [Fact]
    public async Task Should_Have_Error_When_FileNameExceeds500Characters()
    {
        string longFileName = new('x', 501);
        UploadFileCommand command = CreateValidCommand(fileName: longFileName);

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.FileName);
    }

    [Fact]
    public async Task Should_Have_Error_When_ContentTypeIsEmpty()
    {
        UploadFileCommand command = CreateValidCommand(contentType: "");

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.ContentType);
    }

    [Fact]
    public async Task Should_Have_Error_When_ContentTypeExceeds200Characters()
    {
        string longContentType = new string('x', 201);
        UploadFileCommand command = CreateValidCommand(contentType: longContentType);

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.ContentType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Should_Have_Error_When_SizeBytesIsZeroOrNegative(long sizeBytes)
    {
        UploadFileCommand command = CreateValidCommand(sizeBytes: sizeBytes);

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.SizeBytes);
    }

    [Fact]
    public async Task Should_Have_Error_When_ContentIsNull()
    {
        UploadFileCommand command = new(
            Guid.NewGuid(), Guid.NewGuid(), "bucket", "file.txt", "text/plain", null!, 100);

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public async Task Should_Have_Error_When_PathExceeds500Characters()
    {
        string longPath = new('a', 501);
        UploadFileCommand command = CreateValidCommand(path: longPath);

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Path);
    }

    [Fact]
    public async Task Should_Not_Have_Error_When_PathIsNull()
    {
        UploadFileCommand command = CreateValidCommand();

        TestValidationResult<UploadFileCommand> result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Path);
    }

    [Fact]
    public async Task Should_Have_Error_When_JpegContentTypeMismatchesMagicBytes()
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x00, 0x00];
        MemoryStream stream = new(pngBytes);
        UploadFileCommand command = new(
            Guid.NewGuid(), Guid.NewGuid(), "bucket", "fake.jpg", "image/jpeg", stream, pngBytes.Length);

        FluentValidation.Results.ValidationResult result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "File content does not match the declared content type");
    }

    [Fact]
    public async Task Should_Not_Have_Error_When_JpegContentTypeMatchesMagicBytes()
    {
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
        MemoryStream stream = new(jpegBytes);
        UploadFileCommand command = new(
            Guid.NewGuid(), Guid.NewGuid(), "bucket", "photo.jpg", "image/jpeg", stream, jpegBytes.Length);

        FluentValidation.Results.ValidationResult result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Not_Have_Error_When_ContentTypeNotInMagicBytesMap()
    {
        byte[] textBytes = [0x48, 0x65, 0x6C, 0x6C, 0x6F];
        MemoryStream stream = new(textBytes);
        UploadFileCommand command = new(
            Guid.NewGuid(), Guid.NewGuid(), "bucket", "hello.txt", "text/plain", stream, textBytes.Length);

        FluentValidation.Results.ValidationResult result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Have_Error_When_PngContentTypeMismatchesMagicBytes()
    {
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0];
        MemoryStream stream = new(jpegBytes);
        UploadFileCommand command = new(
            Guid.NewGuid(), Guid.NewGuid(), "bucket", "fake.png", "image/png", stream, jpegBytes.Length);

        FluentValidation.Results.ValidationResult result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "File content does not match the declared content type");
    }

    [Fact]
    public async Task Should_Not_Have_Error_When_PdfContentTypeMatchesMagicBytes()
    {
        byte[] pdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D];
        MemoryStream stream = new(pdfBytes);
        UploadFileCommand command = new(
            Guid.NewGuid(), Guid.NewGuid(), "bucket", "doc.pdf", "application/pdf", stream, pdfBytes.Length);

        FluentValidation.Results.ValidationResult result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    private static UploadFileCommand CreateValidCommand(
        Guid? tenantId = null,
        Guid? userId = null,
        string bucketName = "test-bucket",
        string fileName = "test.txt",
        string contentType = "text/plain",
        long sizeBytes = 1024,
        string? path = null)
    {
        return new UploadFileCommand(
            tenantId ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            bucketName,
            fileName,
            contentType,
            new MemoryStream([0x48, 0x65, 0x6C, 0x6C, 0x6F]),
            sizeBytes,
            path);
    }
}
