using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Contracts.Storage;
using Wallow.Storage.Application.Services;
using Wallow.Storage.Domain.Errors;

namespace Wallow.Storage.Api.Controllers;

/// <summary>
/// Serves local presigned URLs. A valid signature over method, key, and expiry
/// is the authorization; no authenticated session is required.
/// Hidden from OpenAPI because callers receive complete URLs from the storage provider.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/storage/local")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class LocalStorageController(IStorageProvider storageProvider, LocalPresignedUrlSigner signer) : ControllerBase
{
    private const string DefaultContentType = "application/octet-stream";

    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    /// <summary>
    /// Download the object at a storage key, authorized solely by a presigned download signature.
    /// </summary>
    /// <param name="key">The storage key the presigned URL addresses.</param>
    /// <param name="expires">Unix timestamp (seconds) after which the URL is dead.</param>
    /// <param name="sig">The signature covering the method, key, and expiry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("files")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        [FromQuery] string key,
        [FromQuery] long expires,
        [FromQuery] string sig,
        CancellationToken cancellationToken)
    {
        if (!signer.Validate(LocalPresignedUrlSigner.DownloadMethod, key, expires, sig))
        {
            return InvalidSignature();
        }

        Stream content;
        try
        {
            content = await storageProvider.DownloadAsync(key, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return this.Problem(StorageErrors.FileNotFound, "No object exists at the requested storage key.");
        }

        if (!_contentTypeProvider.TryGetContentType(key, out string? contentType))
        {
            contentType = DefaultContentType;
        }

        return File(content, contentType);
    }

    /// <summary>
    /// Store the request body at a storage key, authorized solely by a presigned upload signature.
    /// </summary>
    /// <param name="key">The storage key the presigned URL addresses.</param>
    /// <param name="expires">Unix timestamp (seconds) after which the URL is dead.</param>
    /// <param name="sig">The signature covering the method, key, and expiry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPut("files")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Upload(
        [FromQuery] string key,
        [FromQuery] long expires,
        [FromQuery] string sig,
        CancellationToken cancellationToken)
    {
        if (!signer.Validate(LocalPresignedUrlSigner.UploadMethod, key, expires, sig))
        {
            return InvalidSignature();
        }

        await storageProvider.UploadAsync(
            Request.Body,
            key,
            Request.ContentType ?? DefaultContentType,
            cancellationToken);

        return Ok();
    }

    private ProblemResult InvalidSignature() => this.Problem(StorageErrors.SignatureInvalid);
}
