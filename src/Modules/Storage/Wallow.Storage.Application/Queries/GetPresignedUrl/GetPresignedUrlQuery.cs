namespace Wallow.Storage.Application.Queries.GetPresignedUrl;

public sealed record GetPresignedUrlQuery(
    Guid TenantId,
    Guid FileId,
    TimeSpan? Expiry = null);
