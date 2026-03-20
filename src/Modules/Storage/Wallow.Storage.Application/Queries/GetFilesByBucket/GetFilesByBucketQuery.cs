namespace Wallow.Storage.Application.Queries.GetFilesByBucket;

public sealed record GetFilesByBucketQuery(
    Guid TenantId,
    string BucketName,
    string? PathPrefix = null,
    int Page = 1,
    int PageSize = 20);
