using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Application.Configuration;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Enums;
using Foundry.Storage.Domain.Identity;
using Microsoft.Extensions.Options;

namespace Foundry.Storage.Application.Queries.GetPresignedUrl;

public sealed class GetPresignedUrlHandler(
    IStoredFileRepository fileRepository,
    IStorageProvider storageProvider,
    IOptions<PresignedUrlOptions> presignedUrlOptions)
{
    private static readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(1);

    public async Task<Result<PresignedUrlResult>> Handle(
        GetPresignedUrlQuery query,
        CancellationToken cancellationToken)
    {
        StoredFileId fileId = StoredFileId.Create(query.FileId);
        StoredFile? file = await fileRepository.GetByIdAsync(fileId, cancellationToken);

        if (file is null)
        {
            return Result.Failure<PresignedUrlResult>(Error.NotFound("File", query.FileId));
        }

        if (file.TenantId.Value != query.TenantId)
        {
            return Result.Failure<PresignedUrlResult>(Error.NotFound("File", query.FileId));
        }

        if (file.Status != FileStatus.Available)
        {
            return Result.Failure<PresignedUrlResult>(Error.Validation("File.NotAvailable", "File is not yet available for download."));
        }

        TimeSpan maxExpiry = TimeSpan.FromMinutes(presignedUrlOptions.Value.MaxDownloadExpiryMinutes);
        TimeSpan expiry = query.Expiry ?? _defaultExpiry;
        if (expiry > maxExpiry)
        {
            expiry = maxExpiry;
        }

        string url = await storageProvider.GetPresignedUrlAsync(
            file.StorageKey,
            expiry,
            forUpload: false,
            cancellationToken);

        return new PresignedUrlResult(url, DateTime.UtcNow.Add(expiry));
    }
}
