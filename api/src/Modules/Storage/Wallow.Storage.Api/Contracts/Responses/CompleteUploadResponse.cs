namespace Wallow.Storage.Api.Contracts.Responses;

public sealed record CompleteUploadResponse(
    Guid FileId,
    string Status);
