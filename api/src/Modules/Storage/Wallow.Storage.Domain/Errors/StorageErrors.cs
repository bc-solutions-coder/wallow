using Wallow.Shared.Kernel.Errors;

namespace Wallow.Storage.Domain.Errors;

/// <summary>
/// The error catalog the Storage module owns. Registered by <c>AddStorageModule</c>.
/// </summary>
public static class StorageErrors
{
    public static readonly ErrorCatalogEntry BucketNotFound = new(
        "Bucket.NotFound", ErrorKind.NotFound, "Bucket not found");

    public static readonly ErrorCatalogEntry BucketAlreadyExists = new(
        "Bucket.AlreadyExists", ErrorKind.Conflict, "A bucket with that name already exists");

    public static readonly ErrorCatalogEntry BucketNotEmpty = new(
        "Bucket.NotEmpty", ErrorKind.Validation, "The bucket still contains files");

    public static readonly ErrorCatalogEntry FileNotFound = new(
        "File.NotFound", ErrorKind.NotFound, "File not found");

    public static readonly ErrorCatalogEntry FileNotAvailable = new(
        "File.NotAvailable", ErrorKind.BusinessRule, "File is not yet available for download.");

    public static readonly ErrorCatalogEntry FileNotUploaded = new(
        "File.NotUploaded", ErrorKind.BusinessRule, "No object has been uploaded for this file's presigned URL yet.");

    public static readonly ErrorCatalogEntry FileContentTypeNotAllowed = new(
        "File.ContentTypeNotAllowed", ErrorKind.Validation, "That content type is not allowed in this bucket");

    public static readonly ErrorCatalogEntry FileTooLarge = new(
        "File.TooLarge", ErrorKind.Validation, "The file exceeds the bucket's maximum file size");

    public static readonly ErrorCatalogEntry FileExceedsUploadLimit = new(
        "File.ExceedsUploadLimit", ErrorKind.Validation, "The file exceeds the tenant upload limit");

    public static readonly ErrorCatalogEntry FileExtensionNotAllowed = new(
        "File.ExtensionNotAllowed", ErrorKind.Validation, "That file extension is not allowed for this tenant");

    public static readonly ErrorCatalogEntry FileFailedSecurityScan = new(
        "File.FailedSecurityScan", ErrorKind.Validation, "The file failed the security scan");

    public static readonly ErrorCatalogEntry QuotaExceeded = new(
        "Storage.QuotaExceeded", ErrorKind.Validation, "The upload would exceed the tenant storage quota");

    /// <summary>A signed local-storage URL whose signature is missing, expired, or for another request.</summary>
    public static readonly ErrorCatalogEntry SignatureInvalid = new(
        "File.SignatureInvalid", ErrorKind.Forbidden, "The URL's signature is missing, expired, or does not authorize this request.");
}
