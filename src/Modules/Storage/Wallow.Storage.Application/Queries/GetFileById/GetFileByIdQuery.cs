namespace Wallow.Storage.Application.Queries.GetFileById;

public sealed record GetFileByIdQuery(Guid TenantId, Guid FileId);
