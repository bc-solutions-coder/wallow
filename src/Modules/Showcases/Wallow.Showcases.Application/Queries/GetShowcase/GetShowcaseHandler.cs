using Wallow.Shared.Kernel.Results;
using Wallow.Showcases.Application.Contracts;
using Wallow.Showcases.Domain.Entities;

namespace Wallow.Showcases.Application.Queries.GetShowcase;

public sealed class GetShowcaseHandler(IShowcaseRepository repository)
{
    public async Task<Result<ShowcaseDto>> Handle(
        GetShowcaseQuery query,
        CancellationToken ct)
    {
        Showcase? showcase = await repository.GetByIdAsync(query.Id, ct);

        if (showcase is null)
        {
            return Result.Failure<ShowcaseDto>(Error.NotFound("Showcase", query.Id));
        }

        return Result.Success(new ShowcaseDto(
            showcase.Id,
            showcase.Title,
            showcase.Description,
            showcase.Category,
            showcase.DemoUrl,
            showcase.GitHubUrl,
            showcase.VideoUrl,
            showcase.Tags,
            showcase.DisplayOrder,
            showcase.IsPublished));
    }
}
