using Foundry.Shared.Kernel.Results;
using Foundry.Showcases.Application.Contracts;
using Foundry.Showcases.Domain.Entities;
using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;

namespace Foundry.Showcases.Application.Commands.CreateShowcase;

public sealed record CreateShowcaseCommand(
    string Title,
    string? Description,
    ShowcaseCategory Category,
    string? DemoUrl,
    string? GitHubUrl,
    string? VideoUrl,
    IReadOnlyList<string>? Tags = null,
    int DisplayOrder = 0,
    bool IsPublished = false);

public sealed class CreateShowcaseHandler(IShowcaseRepository repository)
{
    public async Task<Result<ShowcaseId>> Handle(
        CreateShowcaseCommand command,
        CancellationToken cancellationToken)
    {
        Result<Showcase> createResult = Showcase.Create(
            command.Title,
            command.Description,
            command.Category,
            command.DemoUrl,
            command.GitHubUrl,
            command.VideoUrl,
            command.Tags,
            command.DisplayOrder,
            command.IsPublished);

        if (createResult.IsFailure)
        {
            return Result.Failure<ShowcaseId>(createResult.Error);
        }

        Showcase showcase = createResult.Value;

        await repository.AddAsync(showcase, cancellationToken);

        return Result.Success(showcase.Id);
    }
}
