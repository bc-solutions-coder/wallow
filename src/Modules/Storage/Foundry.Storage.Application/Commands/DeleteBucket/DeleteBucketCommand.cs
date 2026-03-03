namespace Foundry.Storage.Application.Commands.DeleteBucket;

public sealed record DeleteBucketCommand(
    Guid TenantId,
    string Name,
    bool Force = false);
