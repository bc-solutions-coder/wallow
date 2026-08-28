using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Wallow.Shared.Contracts.Storage;
using Wallow.Storage.Application.Services;

namespace Wallow.Storage.Api.Controllers;

/// <summary>
/// Serves the presigned URLs <c>LocalStorageProvider</c> mints. A real object store answers
/// its presigned URLs itself; the local filesystem cannot, so these key-addressed endpoints
/// stand in. Anonymous by design, exactly like an S3 presigned URL: possession of a valid,
/// unexpired signature over the method + key + expiry is the entire authorization.
/// Hidden from the OpenAPI document for the same reason S3's presigned endpoints appear in
/// no API spec: callers receive the complete URL as an opaque string and can never construct
/// one themselves, so a generated SDK method would be uncallable.
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
            return Problem(
                title: "File not found",
                detail: "No object exists at the requested storage key.",
                statusCode: StatusCodes.Status404NotFound);
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

    private ObjectResult InvalidSignature() =>
        Problem(
            title: "Invalid signature",
            detail: "The URL's signature is missing, expired, or does not authorize this request.",
            statusCode: StatusCodes.Status403Forbidden);
}
