using System.Security.Claims;
using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Contracts.Storage.Commands;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Services;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Api.Contracts.Requests;
using Foundry.Storage.Api.Contracts.Responses;
using Foundry.Storage.Api.Controllers;
using Foundry.Storage.Application.Commands.CreateBucket;
using Foundry.Storage.Application.Commands.DeleteBucket;
using Foundry.Storage.Application.Commands.DeleteFile;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Application.Queries.GetBucketByName;
using Foundry.Storage.Application.Queries.GetFileById;
using Foundry.Storage.Application.Queries.GetFilesByBucket;
using Foundry.Storage.Application.Queries.GetPresignedUrl;
using Foundry.Storage.Application.Queries.GetUploadPresignedUrl;
using Foundry.Storage.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Storage.Tests.Api.Controllers;

public class StorageControllerTests
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;
    private readonly StorageController _controller;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public StorageControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.Create(_tenantId));
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetCurrentUserId().Returns(_userId);

        _controller = new StorageController(_bus, tenantContext, _currentUserService);

        ClaimsPrincipal user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region CreateBucket

    [Fact]
    public async Task CreateBucket_WithValidRequest_Returns201Created()
    {
        CreateBucketRequest request = new("test-bucket", "A test bucket", "Public");
        BucketDto dto = new(Guid.NewGuid(), "test-bucket", "A test bucket", "Public", 0, null, null, false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateBucket(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        BucketResponse response = created.Value.Should().BeOfType<BucketResponse>().Subject;
        response.Name.Should().Be("test-bucket");
        response.Description.Should().Be("A test bucket");
        response.Access.Should().Be("Public");
    }

    [Fact]
    public async Task CreateBucket_WithInvalidAccess_DefaultsToPrivate()
    {
        CreateBucketRequest request = new("test-bucket", Access: "InvalidValue");
        BucketDto dto = new(Guid.NewGuid(), "test-bucket", null, "Private", 0, null, null, false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateBucket(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<BucketDto>>(
            Arg.Is<CreateBucketCommand>(c => c.Access == AccessLevel.Private),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBucket_WithRetentionPolicy_PassesRetentionToCommand()
    {
        CreateBucketRequest request = new("test-bucket", RetentionDays: 30, RetentionAction: "Archive");
        BucketDto dto = new(Guid.NewGuid(), "test-bucket", null, "Private", 0, null,
            new RetentionPolicyDto(30, "Archive"), false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateBucket(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<BucketDto>>(
            Arg.Is<CreateBucketCommand>(c =>
                c.RetentionDays == 30 && c.RetentionAction == RetentionAction.Archive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBucket_WithInvalidRetentionAction_PassesNullRetentionAction()
    {
        CreateBucketRequest request = new("test-bucket", RetentionDays: 30, RetentionAction: "InvalidAction");
        BucketDto dto = new(Guid.NewGuid(), "test-bucket", null, "Private", 0, null, null, false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateBucket(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<BucketDto>>(
            Arg.Is<CreateBucketCommand>(c => c.RetentionAction == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBucket_WithEmptyRetentionAction_PassesNullRetentionAction()
    {
        CreateBucketRequest request = new("test-bucket", RetentionDays: 30, RetentionAction: "");
        BucketDto dto = new(Guid.NewGuid(), "test-bucket", null, "Private", 0, null, null, false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateBucket(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<BucketDto>>(
            Arg.Is<CreateBucketCommand>(c => c.RetentionAction == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBucket_WithRetentionPolicy_MapsRetentionInResponse()
    {
        CreateBucketRequest request = new("test-bucket", RetentionDays: 30, RetentionAction: "Delete");
        BucketDto dto = new(Guid.NewGuid(), "test-bucket", null, "Private", 0, null,
            new RetentionPolicyDto(30, "Delete"), false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateBucket(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        BucketResponse response = created.Value.Should().BeOfType<BucketResponse>().Subject;
        response.Retention.Should().NotBeNull();
        response.Retention!.Days.Should().Be(30);
        response.Retention.Action.Should().Be("Delete");
    }

    [Fact]
    public async Task CreateBucket_WithNoRetentionPolicy_MapsNullRetentionInResponse()
    {
        CreateBucketRequest request = new("test-bucket");
        BucketDto dto = new(Guid.NewGuid(), "test-bucket", null, "Private", 0, null, null, false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateBucket(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        BucketResponse response = created.Value.Should().BeOfType<BucketResponse>().Subject;
        response.Retention.Should().BeNull();
    }

    [Fact]
    public async Task CreateBucket_WithAllowedContentTypes_PassesToCommand()
    {
        List<string> contentTypes = new() { "image/png", "image/jpeg" };
        CreateBucketRequest request = new("test-bucket", AllowedContentTypes: contentTypes);
        BucketDto dto = new(Guid.NewGuid(), "test-bucket", null, "Private", 0, contentTypes, null, false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateBucket(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<BucketDto>>(
            Arg.Is<CreateBucketCommand>(c => c.AllowedContentTypes != null && c.AllowedContentTypes.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBucket_WithVersioning_PassesToCommand()
    {
        CreateBucketRequest request = new("test-bucket", Versioning: true);
        BucketDto dto = new(Guid.NewGuid(), "test-bucket", null, "Private", 0, null, null, true, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateBucket(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<BucketDto>>(
            Arg.Is<CreateBucketCommand>(c => c.Versioning),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBucket_WhenFailure_ReturnsErrorResult()
    {
        CreateBucketRequest request = new("test-bucket");
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<BucketDto>(Error.Conflict("Bucket already exists")));

        IActionResult result = await _controller.CreateBucket(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task CreateBucket_WithMaxFileSize_PassesToCommand()
    {
        CreateBucketRequest request = new("test-bucket", MaxFileSizeBytes: 1024 * 1024);
        BucketDto dto = new(Guid.NewGuid(), "test-bucket", null, "Private", 1024 * 1024, null, null, false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.CreateBucket(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<BucketDto>>(
            Arg.Is<CreateBucketCommand>(c => c.MaxFileSizeBytes == 1024 * 1024),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBucket_SetsLocationHeader()
    {
        CreateBucketRequest request = new("my-bucket");
        BucketDto dto = new(Guid.NewGuid(), "my-bucket", null, "Private", 0, null, null, false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<CreateBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.CreateBucket(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be("/api/v1/storage/buckets/my-bucket");
    }

    #endregion

    #region GetBucket

    [Fact]
    public async Task GetBucket_WhenFound_ReturnsOkWithBucketResponse()
    {
        BucketDto dto = new(Guid.NewGuid(), "test-bucket", "desc", "Public", 0, null, null, false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<GetBucketByNameQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetBucket("test-bucket", CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        BucketResponse response = ok.Value.Should().BeOfType<BucketResponse>().Subject;
        response.Name.Should().Be("test-bucket");
        response.Description.Should().Be("desc");
    }

    [Fact]
    public async Task GetBucket_WhenNotFound_Returns404()
    {
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<GetBucketByNameQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<BucketDto>(Error.NotFound("Bucket", "missing-bucket")));

        IActionResult result = await _controller.GetBucket("missing-bucket", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetBucket_PassesCorrectNameToQuery()
    {
        BucketDto dto = new(Guid.NewGuid(), "my-bucket", null, "Private", 0, null, null, false, DateTime.UtcNow);
        _bus.InvokeAsync<Result<BucketDto>>(Arg.Any<GetBucketByNameQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.GetBucket("my-bucket", CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<BucketDto>>(
            Arg.Is<GetBucketByNameQuery>(q => q.Name == "my-bucket"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region DeleteBucket

    [Fact]
    public async Task DeleteBucket_WhenSuccess_Returns204NoContent()
    {
        _bus.InvokeAsync<Result>(Arg.Any<DeleteBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.DeleteBucket("test-bucket", cancellationToken: CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteBucket_WhenNotFound_Returns404()
    {
        _bus.InvokeAsync<Result>(Arg.Any<DeleteBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Bucket", "missing-bucket")));

        IActionResult result = await _controller.DeleteBucket("missing-bucket", cancellationToken: CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeleteBucket_WithForce_PassesForceFlagToCommand()
    {
        _bus.InvokeAsync<Result>(Arg.Any<DeleteBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.DeleteBucket("test-bucket", force: true, cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<DeleteBucketCommand>(c => c.Name == "test-bucket" && c.Force),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteBucket_WithoutForce_PassesForceFalseToCommand()
    {
        _bus.InvokeAsync<Result>(Arg.Any<DeleteBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.DeleteBucket("test-bucket", cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<DeleteBucketCommand>(c => c.Name == "test-bucket" && !c.Force),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteBucket_WhenFailure_ReturnsErrorResult()
    {
        _bus.InvokeAsync<Result>(Arg.Any<DeleteBucketCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Validation("Bucket is not empty")));

        IActionResult result = await _controller.DeleteBucket("test-bucket", cancellationToken: CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region Upload

    [Fact]
    public async Task Upload_WithValidFile_Returns201Created()
    {
        Guid fileId = Guid.NewGuid();
        IFormFile file = CreateMockFormFile("test.txt", "text/plain", 100);
        UploadResult uploadResult = new(fileId, "test.txt", "key", 100, "text/plain", DateTime.UtcNow);
        _bus.InvokeAsync<Result<UploadResult>>(Arg.Any<UploadFileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(uploadResult));

        IActionResult result = await _controller.Upload(file, "test-bucket", cancellationToken: CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        UploadResponse response = created.Value.Should().BeOfType<UploadResponse>().Subject;
        response.FileId.Should().Be(fileId);
        response.FileName.Should().Be("test.txt");
        response.SizeBytes.Should().Be(100);
        response.ContentType.Should().Be("text/plain");
    }

    [Fact]
    public async Task Upload_WithEmptyFile_Returns400BadRequest()
    {
        IFormFile file = CreateMockFormFile("empty.txt", "text/plain", 0);

        IActionResult result = await _controller.Upload(file, "test-bucket", cancellationToken: CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("File is empty");
    }

    [Fact]
    public async Task Upload_PassesTenantIdFromContext()
    {
        IFormFile file = CreateMockFormFile("test.txt", "text/plain", 100);
        UploadResult uploadResult = new(Guid.NewGuid(), "test.txt", "key", 100, "text/plain", DateTime.UtcNow);
        _bus.InvokeAsync<Result<UploadResult>>(Arg.Any<UploadFileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(uploadResult));

        await _controller.Upload(file, "test-bucket", cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<UploadResult>>(
            Arg.Is<UploadFileCommand>(c => c.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_PassesUserIdFromClaims()
    {
        IFormFile file = CreateMockFormFile("test.txt", "text/plain", 100);
        UploadResult uploadResult = new(Guid.NewGuid(), "test.txt", "key", 100, "text/plain", DateTime.UtcNow);
        _bus.InvokeAsync<Result<UploadResult>>(Arg.Any<UploadFileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(uploadResult));

        await _controller.Upload(file, "test-bucket", cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<UploadResult>>(
            Arg.Is<UploadFileCommand>(c => c.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_WithPath_PassesPathToCommand()
    {
        IFormFile file = CreateMockFormFile("test.txt", "text/plain", 100);
        UploadResult uploadResult = new(Guid.NewGuid(), "test.txt", "key", 100, "text/plain", DateTime.UtcNow);
        _bus.InvokeAsync<Result<UploadResult>>(Arg.Any<UploadFileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(uploadResult));

        await _controller.Upload(file, "test-bucket", path: "images/avatars", cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<UploadResult>>(
            Arg.Is<UploadFileCommand>(c => c.Path == "images/avatars"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_WithIsPublic_PassesIsPublicToCommand()
    {
        IFormFile file = CreateMockFormFile("test.txt", "text/plain", 100);
        UploadResult uploadResult = new(Guid.NewGuid(), "test.txt", "key", 100, "text/plain", DateTime.UtcNow);
        _bus.InvokeAsync<Result<UploadResult>>(Arg.Any<UploadFileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(uploadResult));

        await _controller.Upload(file, "test-bucket", isPublic: true, cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<UploadResult>>(
            Arg.Is<UploadFileCommand>(c => c.IsPublic),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_WhenFailure_ThrowsDueToValueAccess()
    {
        // BUG: Controller accesses result.Value?.FileId for location string even on failure path,
        // which throws InvalidOperationException from the Result type.
        IFormFile file = CreateMockFormFile("test.txt", "text/plain", 100);
        _bus.InvokeAsync<Result<UploadResult>>(Arg.Any<UploadFileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<UploadResult>(Error.NotFound("Bucket", "test-bucket")));

        Func<Task> act = () => _controller.Upload(file, "test-bucket", cancellationToken: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot access value of a failed result");
    }

    [Fact]
    public async Task Upload_WithNoUserClaims_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IFormFile file = CreateMockFormFile("test.txt", "text/plain", 100);

        IActionResult result = await _controller.Upload(file, "test-bucket", cancellationToken: CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Upload_WithSubClaim_UsesSubClaimAsUserId()
    {
        Guid subUserId = Guid.NewGuid();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("sub", subUserId.ToString())
                }, "TestAuth"))
            }
        };
        _currentUserService.GetCurrentUserId().Returns(subUserId);
        IFormFile file = CreateMockFormFile("test.txt", "text/plain", 100);
        UploadResult uploadResult = new(Guid.NewGuid(), "test.txt", "key", 100, "text/plain", DateTime.UtcNow);
        _bus.InvokeAsync<Result<UploadResult>>(Arg.Any<UploadFileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(uploadResult));

        await _controller.Upload(file, "test-bucket", cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<UploadResult>>(
            Arg.Is<UploadFileCommand>(c => c.UserId == subUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_WithNonGuidUserClaim_ReturnsUnauthorized()
    {
        _currentUserService.GetCurrentUserId().Returns((Guid?)null);
        IFormFile file = CreateMockFormFile("test.txt", "text/plain", 100);

        IActionResult result = await _controller.Upload(file, "test-bucket", cancellationToken: CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region GetFile

    [Fact]
    public async Task GetFile_WhenFound_ReturnsOkWithFileMetadata()
    {
        Guid fileId = Guid.NewGuid();
        Guid bucketId = Guid.NewGuid();
        StoredFileDto dto = new(fileId, _tenantId, bucketId, "test.txt", "text/plain", 100, "key",
            "docs/", true, _userId, DateTime.UtcNow, null);
        _bus.InvokeAsync<Result<StoredFileDto>>(Arg.Any<GetFileByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetFile(fileId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        FileMetadataResponse response = ok.Value.Should().BeOfType<FileMetadataResponse>().Subject;
        response.Id.Should().Be(fileId);
        response.BucketId.Should().Be(bucketId);
        response.FileName.Should().Be("test.txt");
        response.ContentType.Should().Be("text/plain");
        response.SizeBytes.Should().Be(100);
        response.Path.Should().Be("docs/");
        response.IsPublic.Should().BeTrue();
        response.UploadedBy.Should().Be(_userId);
    }

    [Fact]
    public async Task GetFile_WhenNotFound_Returns404()
    {
        Guid fileId = Guid.NewGuid();
        _bus.InvokeAsync<Result<StoredFileDto>>(Arg.Any<GetFileByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<StoredFileDto>(Error.NotFound("File", fileId)));

        IActionResult result = await _controller.GetFile(fileId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetFile_PassesTenantIdAndFileIdToQuery()
    {
        Guid fileId = Guid.NewGuid();
        StoredFileDto dto = new(fileId, _tenantId, Guid.NewGuid(), "test.txt", "text/plain", 100, "key",
            null, false, _userId, DateTime.UtcNow, null);
        _bus.InvokeAsync<Result<StoredFileDto>>(Arg.Any<GetFileByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.GetFile(fileId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<StoredFileDto>>(
            Arg.Is<GetFileByIdQuery>(q => q.TenantId == _tenantId && q.FileId == fileId),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Download

    [Fact]
    public async Task Download_WhenFound_ReturnsRedirect()
    {
        Guid fileId = Guid.NewGuid();
        PresignedUrlResult urlResult = new("https://storage.example.com/file?token=abc", DateTime.UtcNow.AddHours(1));
        _bus.InvokeAsync<Result<PresignedUrlResult>>(Arg.Any<GetPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(urlResult));

        IActionResult result = await _controller.Download(fileId, CancellationToken.None);

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("https://storage.example.com/file?token=abc");
    }

    [Fact]
    public async Task Download_WhenNotFound_Returns404()
    {
        Guid fileId = Guid.NewGuid();
        _bus.InvokeAsync<Result<PresignedUrlResult>>(Arg.Any<GetPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PresignedUrlResult>(Error.NotFound("File", fileId)));

        IActionResult result = await _controller.Download(fileId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Download_PassesTenantIdAndFileIdToQuery()
    {
        Guid fileId = Guid.NewGuid();
        PresignedUrlResult urlResult = new("https://example.com/file", DateTime.UtcNow.AddHours(1));
        _bus.InvokeAsync<Result<PresignedUrlResult>>(Arg.Any<GetPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(urlResult));

        await _controller.Download(fileId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PresignedUrlResult>>(
            Arg.Is<GetPresignedUrlQuery>(q => q.TenantId == _tenantId && q.FileId == fileId),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Delete_WhenSuccess_Returns204NoContent()
    {
        Guid fileId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteFileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.Delete(fileId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_WhenNotFound_Returns404()
    {
        Guid fileId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteFileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("File", fileId)));

        IActionResult result = await _controller.Delete(fileId, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Delete_PassesTenantIdAndFileIdToCommand()
    {
        Guid fileId = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteFileCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.Delete(fileId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<DeleteFileCommand>(c => c.TenantId == _tenantId && c.FileId == fileId),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region ListFiles

    [Fact]
    public async Task ListFiles_WhenFound_ReturnsOkWithFileList()
    {
        List<StoredFileDto> files = new()
        {
            new(Guid.NewGuid(), _tenantId, Guid.NewGuid(), "file1.txt", "text/plain", 100, "key1",
                null, false, _userId, DateTime.UtcNow, null),
            new(Guid.NewGuid(), _tenantId, Guid.NewGuid(), "file2.txt", "text/plain", 200, "key2",
                null, false, _userId, DateTime.UtcNow, null)
        };
        _bus.InvokeAsync<Result<IReadOnlyList<StoredFileDto>>>(Arg.Any<GetFilesByBucketQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<StoredFileDto>>(files));

        IActionResult result = await _controller.ListFiles("test-bucket", cancellationToken: CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<FileMetadataResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<FileMetadataResponse>>().Subject;
        responses.Should().HaveCount(2);
        responses[0].FileName.Should().Be("file1.txt");
        responses[1].FileName.Should().Be("file2.txt");
    }

    [Fact]
    public async Task ListFiles_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<StoredFileDto>>>(Arg.Any<GetFilesByBucketQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<StoredFileDto>>(new List<StoredFileDto>()));

        IActionResult result = await _controller.ListFiles("test-bucket", cancellationToken: CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<FileMetadataResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<FileMetadataResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task ListFiles_WithPath_PassesPathToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<StoredFileDto>>>(Arg.Any<GetFilesByBucketQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<StoredFileDto>>(new List<StoredFileDto>()));

        await _controller.ListFiles("test-bucket", path: "images/", cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<StoredFileDto>>>(
            Arg.Is<GetFilesByBucketQuery>(q => q.BucketName == "test-bucket" && q.PathPrefix == "images/"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListFiles_PassesTenantIdToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<StoredFileDto>>>(Arg.Any<GetFilesByBucketQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<StoredFileDto>>(new List<StoredFileDto>()));

        await _controller.ListFiles("test-bucket", cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<StoredFileDto>>>(
            Arg.Is<GetFilesByBucketQuery>(q => q.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListFiles_WhenBucketNotFound_Returns404()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<StoredFileDto>>>(Arg.Any<GetFilesByBucketQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IReadOnlyList<StoredFileDto>>(Error.NotFound("Bucket", "missing")));

        IActionResult result = await _controller.ListFiles("missing", cancellationToken: CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    #endregion

    #region GetPresignedUploadUrl

    [Fact]
    public async Task GetPresignedUploadUrl_WithValidRequest_ReturnsOk()
    {
        PresignedUploadRequest request = new("test-bucket", "test.txt", "text/plain", 1024);
        DateTime expiresAt = DateTime.UtcNow.AddHours(1);
        PresignedUploadResult uploadResult = new(Guid.NewGuid(), "https://storage.example.com/upload", "storage-key", expiresAt);
        _bus.InvokeAsync<Result<PresignedUploadResult>>(Arg.Any<GetUploadPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(uploadResult));

        IActionResult result = await _controller.GetPresignedUploadUrl(request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        PresignedUploadResponse response = ok.Value.Should().BeOfType<PresignedUploadResponse>().Subject;
        response.UploadUrl.Should().Be("https://storage.example.com/upload");
        response.StorageKey.Should().Be("storage-key");
        response.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task GetPresignedUploadUrl_WithExpiryMinutes_PassesExpiryToQuery()
    {
        PresignedUploadRequest request = new("test-bucket", "test.txt", "text/plain", 1024, ExpiryMinutes: 60);
        PresignedUploadResult uploadResult = new(Guid.NewGuid(), "url", "key", DateTime.UtcNow.AddHours(1));
        _bus.InvokeAsync<Result<PresignedUploadResult>>(Arg.Any<GetUploadPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(uploadResult));

        await _controller.GetPresignedUploadUrl(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PresignedUploadResult>>(
            Arg.Is<GetUploadPresignedUrlQuery>(q => q.Expiry == TimeSpan.FromMinutes(60)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPresignedUploadUrl_WithoutExpiryMinutes_PassesNullExpiry()
    {
        PresignedUploadRequest request = new("test-bucket", "test.txt", "text/plain", 1024);
        PresignedUploadResult uploadResult = new(Guid.NewGuid(), "url", "key", DateTime.UtcNow.AddHours(1));
        _bus.InvokeAsync<Result<PresignedUploadResult>>(Arg.Any<GetUploadPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(uploadResult));

        await _controller.GetPresignedUploadUrl(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PresignedUploadResult>>(
            Arg.Is<GetUploadPresignedUrlQuery>(q => q.Expiry == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPresignedUploadUrl_WithPath_PassesPathToQuery()
    {
        PresignedUploadRequest request = new("test-bucket", "test.txt", "text/plain", 1024, Path: "uploads/");
        PresignedUploadResult uploadResult = new(Guid.NewGuid(), "url", "key", DateTime.UtcNow.AddHours(1));
        _bus.InvokeAsync<Result<PresignedUploadResult>>(Arg.Any<GetUploadPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(uploadResult));

        await _controller.GetPresignedUploadUrl(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PresignedUploadResult>>(
            Arg.Is<GetUploadPresignedUrlQuery>(q => q.Path == "uploads/"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPresignedUploadUrl_PassesTenantIdToQuery()
    {
        PresignedUploadRequest request = new("test-bucket", "test.txt", "text/plain", 1024);
        PresignedUploadResult uploadResult = new(Guid.NewGuid(), "url", "key", DateTime.UtcNow.AddHours(1));
        _bus.InvokeAsync<Result<PresignedUploadResult>>(Arg.Any<GetUploadPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(uploadResult));

        await _controller.GetPresignedUploadUrl(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PresignedUploadResult>>(
            Arg.Is<GetUploadPresignedUrlQuery>(q => q.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPresignedUploadUrl_WhenFailure_ReturnsErrorResult()
    {
        PresignedUploadRequest request = new("missing-bucket", "test.txt", "text/plain", 1024);
        _bus.InvokeAsync<Result<PresignedUploadResult>>(Arg.Any<GetUploadPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PresignedUploadResult>(Error.NotFound("Bucket", "missing-bucket")));

        IActionResult result = await _controller.GetPresignedUploadUrl(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    #endregion

    #region GetPresignedDownloadUrl

    [Fact]
    public async Task GetPresignedDownloadUrl_WhenFound_ReturnsOk()
    {
        Guid fileId = Guid.NewGuid();
        DateTime expiresAt = DateTime.UtcNow.AddHours(1);
        PresignedUrlResult urlResult = new("https://storage.example.com/download", expiresAt);
        _bus.InvokeAsync<Result<PresignedUrlResult>>(Arg.Any<GetPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(urlResult));

        IActionResult result = await _controller.GetPresignedDownloadUrl(fileId, cancellationToken: CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        PresignedUrlResponse response = ok.Value.Should().BeOfType<PresignedUrlResponse>().Subject;
        response.Url.Should().Be("https://storage.example.com/download");
        response.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task GetPresignedDownloadUrl_WithExpiryMinutes_PassesExpiryToQuery()
    {
        Guid fileId = Guid.NewGuid();
        PresignedUrlResult urlResult = new("url", DateTime.UtcNow.AddMinutes(30));
        _bus.InvokeAsync<Result<PresignedUrlResult>>(Arg.Any<GetPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(urlResult));

        await _controller.GetPresignedDownloadUrl(fileId, expiryMinutes: 30, cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PresignedUrlResult>>(
            Arg.Is<GetPresignedUrlQuery>(q => q.Expiry == TimeSpan.FromMinutes(30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPresignedDownloadUrl_WithoutExpiryMinutes_PassesNullExpiry()
    {
        Guid fileId = Guid.NewGuid();
        PresignedUrlResult urlResult = new("url", DateTime.UtcNow.AddHours(1));
        _bus.InvokeAsync<Result<PresignedUrlResult>>(Arg.Any<GetPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(urlResult));

        await _controller.GetPresignedDownloadUrl(fileId, cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PresignedUrlResult>>(
            Arg.Is<GetPresignedUrlQuery>(q => q.Expiry == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPresignedDownloadUrl_WhenNotFound_Returns404()
    {
        Guid fileId = Guid.NewGuid();
        _bus.InvokeAsync<Result<PresignedUrlResult>>(Arg.Any<GetPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PresignedUrlResult>(Error.NotFound("File", fileId)));

        IActionResult result = await _controller.GetPresignedDownloadUrl(fileId, cancellationToken: CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetPresignedDownloadUrl_PassesTenantIdToQuery()
    {
        Guid fileId = Guid.NewGuid();
        PresignedUrlResult urlResult = new("url", DateTime.UtcNow.AddHours(1));
        _bus.InvokeAsync<Result<PresignedUrlResult>>(Arg.Any<GetPresignedUrlQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(urlResult));

        await _controller.GetPresignedDownloadUrl(fileId, cancellationToken: CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<PresignedUrlResult>>(
            Arg.Is<GetPresignedUrlQuery>(q => q.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helpers

    private static IFormFile CreateMockFormFile(string fileName, string contentType, long length)
    {
        IFormFile file = Substitute.For<IFormFile>();
        file.FileName.Returns(fileName);
        file.ContentType.Returns(contentType);
        file.Length.Returns(length);
        file.OpenReadStream().Returns(new MemoryStream(new byte[length]));
        return file;
    }

    #endregion
}
