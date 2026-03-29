namespace Wallow.Storage.Application.Queries.GetPresignedUrl;

public sealed record GetPresignedUrlQuery(
    Guid FileId,
    TimeSpan? Expiry = null);
