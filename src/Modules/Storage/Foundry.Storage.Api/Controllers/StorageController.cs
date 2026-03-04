using Asp.Versioning;
using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Contracts.Storage.Commands;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Pagination;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Api.Contracts.Requests;
using Foundry.Storage.Api.Contracts.Responses;
using Foundry.Shared.Api.Extensions;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.Services;
using Wolverine;

namespace Foundry.Storage.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/storage")]
[Authorize]
[Tags("Storage")]
[Produces("application/json")]
public sealed class StorageController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;

    public StorageController(IMessageBus bus, ITenantContext tenantContext, ICurrentUserService currentUserService)
    {
        _bus = bus;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
    }

    #region Bucket Operations

    /// <summary>
    /// Create a new storage bucket.
    /// </summary>
    [HttpPost("buckets")]
    [HasPermission(PermissionType.StorageWrite)]
    [ProducesResponseType(typeof(BucketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBucket(
        [FromBody] CreateBucketRequest request,
        CancellationToken cancellationToken)
    {
        AccessLevel access = Enum.TryParse<AccessLevel>(request.Access, true, out AccessLevel accessLevel)
            ? accessLevel
            : AccessLevel.Private;

        RetentionAction? retentionAction = null;
        if (!string.IsNullOrEmpty(request.RetentionAction) &&
            Enum.TryParse<RetentionAction>(request.RetentionAction, true, out RetentionAction action))
        {
            retentionAction = action;
        }

        CreateBucketCommand command = new CreateBucketCommand(
            request.Name,
            request.Description,
            access,
            request.MaxFileSizeBytes,
            request.AllowedContentTypes,
            request.RetentionDays,
            retentionAction,
            request.Versioning);

        Result<BucketDto> result = await _bus.InvokeAsync<Result<BucketDto>>(command, cancellationToken);

        return result.Map(ToBucketResponse)
            .ToCreatedResult($"/api/v1/storage/buckets/{request.Name}");
    }

    /// <summary>
    /// Get bucket by name.
    /// </summary>
    [HttpGet("buckets/{name}")]
    [HasPermission(PermissionType.StorageRead)]
    [ProducesResponseType(typeof(BucketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBucket(string name, CancellationToken cancellationToken)
    {
        Result<BucketDto> result = await _bus.InvokeAsync<Result<BucketDto>>(
            new GetBucketByNameQuery(name), cancellationToken);

        return result.Map(ToBucketResponse).ToActionResult();
    }

    /// <summary>
    /// Delete a bucket.
    /// </summary>
    [HttpDelete("buckets/{name}")]
    [HasPermission(PermissionType.StorageWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBucket(
        string name,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        Result result = await _bus.InvokeAsync<Result>(
            new DeleteBucketCommand(_tenantContext.TenantId.Value, name, force), cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.ToActionResult();
    }

    #endregion

    #region File Operations

    /// <summary>
    /// Upload a file.
    /// </summary>
    [HttpPost("upload")]
    [HasPermission(PermissionType.StorageWrite)]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string bucket,
        [FromForm] string? path = null,
        [FromForm] bool isPublic = false,
        CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "File is empty"
            });
        }

        Guid? userId = _currentUserService.GetCurrentUserId();
        if (userId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        await using Stream stream = file.OpenReadStream();

        UploadFileCommand command = new UploadFileCommand(
            _tenantContext.TenantId.Value,
            userId.Value,
            bucket,
            file.FileName,
            file.ContentType,
            stream,
            file.Length,
            path,
            isPublic);

        Result<UploadResult> result = await _bus.InvokeAsync<Result<UploadResult>>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return result.Map(ToUploadResponse)
            .ToCreatedResult($"/api/v1/storage/files/{result.Value.FileId}");
    }

    /// <summary>
    /// Get file metadata by ID.
    /// </summary>
    [HttpGet("files/{id:guid}")]
    [HasPermission(PermissionType.StorageRead)]
    [ProducesResponseType(typeof(FileMetadataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFile(Guid id, CancellationToken cancellationToken)
    {
        Result<StoredFileDto> result = await _bus.InvokeAsync<Result<StoredFileDto>>(
            new GetFileByIdQuery(_tenantContext.TenantId.Value, id), cancellationToken);

        return result.Map(ToFileMetadataResponse).ToActionResult();
    }

    /// <summary>
    /// Download a file (redirects to presigned URL).
    /// </summary>
    [HttpGet("files/{id:guid}/download")]
    [HasPermission(PermissionType.StorageRead)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        Result<PresignedUrlResult> result = await _bus.InvokeAsync<Result<PresignedUrlResult>>(
            new GetPresignedUrlQuery(_tenantContext.TenantId.Value, id), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Redirect(result.Value.Url);
    }

    /// <summary>
    /// Delete a file.
    /// </summary>
    [HttpDelete("files/{id:guid}")]
    [HasPermission(PermissionType.StorageWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        Result result = await _bus.InvokeAsync<Result>(
            new DeleteFileCommand(_tenantContext.TenantId.Value, id), cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.ToActionResult();
    }

    /// <summary>
    /// List files in a bucket.
    /// </summary>
    [HttpGet("files")]
    [HasPermission(PermissionType.StorageRead)]
    [ProducesResponseType(typeof(PagedResult<FileMetadataResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListFiles(
        [FromQuery] string bucket,
        [FromQuery] string? path = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResult<StoredFileDto>> result = await _bus.InvokeAsync<Result<PagedResult<StoredFileDto>>>(
            new GetFilesByBucketQuery(_tenantContext.TenantId.Value, bucket, path, page, pageSize), cancellationToken);

        return result.Map(paged => new PagedResult<FileMetadataResponse>(
                paged.Items.Select(ToFileMetadataResponse).ToList(),
                paged.TotalCount,
                paged.Page,
                paged.PageSize))
            .ToActionResult();
    }

    #endregion

    #region Presigned URLs

    /// <summary>
    /// Get a presigned URL for direct upload to storage.
    /// </summary>
    [HttpPost("presigned-upload")]
    [HasPermission(PermissionType.StorageWrite)]
    [ProducesResponseType(typeof(PresignedUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPresignedUploadUrl(
        [FromBody] PresignedUploadRequest request,
        CancellationToken cancellationToken)
    {
        TimeSpan? expiry = request.ExpiryMinutes.HasValue
            ? TimeSpan.FromMinutes(request.ExpiryMinutes.Value)
            : null;

        Guid userId = _currentUserService.GetCurrentUserId() ?? Guid.Empty;

        Result<PresignedUploadResult> result = await _bus.InvokeAsync<Result<PresignedUploadResult>>(
            new GetUploadPresignedUrlQuery(
                _tenantContext.TenantId.Value,
                userId,
                request.BucketName,
                request.FileName,
                request.ContentType,
                request.SizeBytes,
                request.Path,
                expiry),
            cancellationToken);

        return result.Map(r => new PresignedUploadResponse(r.FileId, r.UploadUrl, r.ExpiresAt))
            .ToActionResult();
    }

    /// <summary>
    /// Get a presigned URL for downloading a file.
    /// </summary>
    [HttpGet("files/{id:guid}/presigned-url")]
    [HasPermission(PermissionType.StorageRead)]
    [ProducesResponseType(typeof(PresignedUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPresignedDownloadUrl(
        Guid id,
        [FromQuery] int? expiryMinutes = null,
        CancellationToken cancellationToken = default)
    {
        TimeSpan? expiry = expiryMinutes.HasValue
            ? TimeSpan.FromMinutes(expiryMinutes.Value)
            : null;

        Result<PresignedUrlResult> result = await _bus.InvokeAsync<Result<PresignedUrlResult>>(
            new GetPresignedUrlQuery(_tenantContext.TenantId.Value, id, expiry), cancellationToken);

        return result.Map(r => new PresignedUrlResponse(r.Url, r.ExpiresAt))
            .ToActionResult();
    }

    #endregion

    #region Helpers

    private static BucketResponse ToBucketResponse(BucketDto dto) => new(
        dto.Id,
        dto.Name,
        dto.Description,
        dto.Access,
        dto.MaxFileSizeBytes,
        dto.AllowedContentTypes,
        dto.Retention is not null ? new RetentionPolicyResponse(dto.Retention.Days, dto.Retention.Action) : null,
        dto.Versioning,
        dto.CreatedAt);

    private static UploadResponse ToUploadResponse(UploadResult result) => new(
        result.FileId,
        result.FileName,
        result.SizeBytes,
        result.ContentType,
        result.UploadedAt);

    private static FileMetadataResponse ToFileMetadataResponse(StoredFileDto dto) => new(
        dto.Id,
        dto.BucketId,
        dto.FileName,
        dto.ContentType,
        dto.SizeBytes,
        dto.Path,
        dto.IsPublic,
        dto.UploadedBy,
        dto.UploadedAt);

    #endregion
}
