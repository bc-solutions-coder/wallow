using Wallow.Shared.Kernel.Results;
using Wallow.Showcases.Application.Contracts;
using Wallow.Showcases.Domain.Entities;

namespace Wallow.Showcases.Application.Queries.GetShowcases;

public sealed class GetShowcasesHandler(IShowcaseRepository repository)
{
    public async Task<Result<IReadOnlyList<ShowcaseDto>>> Handle(
        GetShowcasesQuery query,
        CancellationToken ct)
    {
        IReadOnlyList<Showcase> showcases = await repository.GetAllAsync(query.Category, query.Tag, ct);
        List<ShowcaseDto> dtos = showcases.Select(s => new ShowcaseDto(
            s.Id,
            s.Title,
            s.Description,
            s.Category,
            s.DemoUrl,
            s.GitHubUrl,
            s.VideoUrl,
            s.Tags,
            s.DisplayOrder,
            s.IsPublished)).ToList();
        return Result.Success<IReadOnlyList<ShowcaseDto>>(dtos);
    }
}
