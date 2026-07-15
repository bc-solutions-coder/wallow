using FluentValidation.TestHelper;
using Wallow.Storage.Application.Queries.GetUploadPresignedUrl;

namespace Wallow.Storage.Tests.Application.Queries.GetUploadPresignedUrl;

public class GetUploadPresignedUrlValidatorTests
{
    private readonly GetUploadPresignedUrlValidator _validator = new();

    [Fact]
    public async Task Should_Not_Have_Error_When_ValidQuery()
    {
        GetUploadPresignedUrlQuery query = CreateValidQuery();

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldNotHaveValidationErrorFor(x => x.TenantId);
        result.ShouldNotHaveValidationErrorFor(x => x.UserId);
        result.ShouldNotHaveValidationErrorFor(x => x.BucketName);
        result.ShouldNotHaveValidationErrorFor(x => x.FileName);
        result.ShouldNotHaveValidationErrorFor(x => x.ContentType);
        result.ShouldNotHaveValidationErrorFor(x => x.SizeBytes);
    }

    [Fact]
    public async Task Should_Have_Error_When_TenantIdIsEmpty()
    {
        GetUploadPresignedUrlQuery query = CreateValidQuery(tenantId: Guid.Empty);

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public async Task Should_Have_Error_When_UserIdIsEmpty()
    {
        GetUploadPresignedUrlQuery query = CreateValidQuery(userId: Guid.Empty);

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_Have_Error_When_BucketNameIsEmpty(string? bucketName)
    {
        GetUploadPresignedUrlQuery query = CreateValidQuery(bucketName: bucketName!);

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldHaveValidationErrorFor(x => x.BucketName);
    }

    [Fact]
    public async Task Should_Have_Error_When_BucketNameExceeds100Characters()
    {
        string longName = new('a', 101);
        GetUploadPresignedUrlQuery query = CreateValidQuery(bucketName: longName);

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldHaveValidationErrorFor(x => x.BucketName);
    }

    [Fact]
    public async Task Should_Skip_FileName_Rules_When_FileNameIsEmpty()
    {
        // The .When() condition skips the entire FileName rule chain for empty values
        GetUploadPresignedUrlQuery query = CreateValidQuery(fileName: "");

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldNotHaveValidationErrorFor(x => x.FileName);
    }

    [Fact]
    public async Task Should_Skip_FileName_Rules_When_FileNameIsNull()
    {
        // The .When() condition skips the entire FileName rule chain for null values
        GetUploadPresignedUrlQuery query = CreateValidQuery(fileName: null!);

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldNotHaveValidationErrorFor(x => x.FileName);
    }

    [Fact]
    public async Task Should_Have_Error_When_FileNameExceeds500Characters()
    {
        string longFileName = new('x', 501);
        GetUploadPresignedUrlQuery query = CreateValidQuery(fileName: longFileName);

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldHaveValidationErrorFor(x => x.FileName);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("\\\\server\\share")]
    [InlineData("C:\\Windows\\file.txt")]
    public async Task Should_Have_Error_When_FileNameContainsPathTraversal(string fileName)
    {
        GetUploadPresignedUrlQuery query = CreateValidQuery(fileName: fileName);

        FluentValidation.Results.ValidationResult result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "File name must not contain path traversal sequences");
    }

    [Fact]
    public async Task Should_Have_Error_When_ContentTypeIsEmpty()
    {
        GetUploadPresignedUrlQuery query = CreateValidQuery(contentType: "");

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldHaveValidationErrorFor(x => x.ContentType);
    }

    [Fact]
    public async Task Should_Have_Error_When_ContentTypeExceeds200Characters()
    {
        string longContentType = new('x', 201);
        GetUploadPresignedUrlQuery query = CreateValidQuery(contentType: longContentType);

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldHaveValidationErrorFor(x => x.ContentType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Should_Have_Error_When_SizeBytesIsZeroOrNegative(long sizeBytes)
    {
        GetUploadPresignedUrlQuery query = CreateValidQuery(sizeBytes: sizeBytes);

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldHaveValidationErrorFor(x => x.SizeBytes);
    }

    [Fact]
    public async Task Should_Have_Error_When_PathExceeds500Characters()
    {
        string longPath = new('a', 501);
        GetUploadPresignedUrlQuery query = CreateValidQuery(path: longPath);

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldHaveValidationErrorFor(x => x.Path);
    }

    [Fact]
    public async Task Should_Not_Have_Error_When_PathIsNull()
    {
        GetUploadPresignedUrlQuery query = CreateValidQuery();

        TestValidationResult<GetUploadPresignedUrlQuery> result = await _validator.TestValidateAsync(query);

        result.ShouldNotHaveValidationErrorFor(x => x.Path);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("/root/data")]
    [InlineData("C:\\data")]
    public async Task Should_Have_Error_When_PathContainsPathTraversal(string path)
    {
        GetUploadPresignedUrlQuery query = CreateValidQuery(path: path);

        FluentValidation.Results.ValidationResult result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Path must not contain path traversal sequences");
    }

    private static GetUploadPresignedUrlQuery CreateValidQuery(
        Guid? tenantId = null,
        Guid? userId = null,
        string bucketName = "test-bucket",
        string fileName = "test.txt",
        string contentType = "text/plain",
        long sizeBytes = 1024,
        string? path = null)
    {
        return new GetUploadPresignedUrlQuery(
            tenantId ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid(),
            bucketName,
            fileName,
            contentType,
            sizeBytes,
            path);
    }
}
