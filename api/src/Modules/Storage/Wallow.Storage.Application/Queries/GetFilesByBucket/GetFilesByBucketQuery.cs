namespace Wallow.Storage.Application.Queries.GetFilesByBucket;

public sealed record GetFilesByBucketQuery(
    string BucketName,
    string? PathPrefix = null,
    int Page = 1,
    int PageSize = 20);
