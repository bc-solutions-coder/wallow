using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.DTOs;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Application.Mappings;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Identity;

namespace Wallow.Storage.Application.Queries.GetFileById;

public sealed class GetFileByIdHandler(IStoredFileRepository fileRepository)
{
    public async Task<Result<StoredFileDto>> Handle(
        GetFileByIdQuery query,
        CancellationToken cancellationToken)
    {
        StoredFileId fileId = StoredFileId.Create(query.FileId);
        StoredFile? file = await fileRepository.GetByIdAsync(fileId, cancellationToken);

        if (file is null)
        {
            return Result.Failure<StoredFileDto>(Error.NotFound("File", query.FileId));
        }

        return Result.Success(file.ToDto());
    }
}
