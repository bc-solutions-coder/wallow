using Wallow.Shared.Kernel.Results;
using Wallow.Showcases.Application.Contracts;
using Wallow.Showcases.Domain.Entities;
using Wallow.Showcases.Domain.Enums;
using Wallow.Showcases.Domain.Identity;

namespace Wallow.Showcases.Application.Commands.UpdateShowcase;

public sealed record UpdateShowcaseCommand(
    ShowcaseId ShowcaseId,
    string Title,
    string? Description,
    ShowcaseCategory Category,
    string? DemoUrl,
    string? GitHubUrl,
    string? VideoUrl,
    IReadOnlyList<string>? Tags = null,
    int DisplayOrder = 0,
    bool IsPublished = false);

public sealed class UpdateShowcaseHandler(IShowcaseRepository repository)
{
    public async Task<Result> Handle(
        UpdateShowcaseCommand command,
        CancellationToken cancellationToken)
    {
        Showcase? showcase = await repository.GetByIdAsync(command.ShowcaseId, cancellationToken);

        if (showcase is null)
        {
            return Result.Failure(Error.NotFound("Showcase.NotFound",
                $"Showcase with ID '{command.ShowcaseId.Value}' was not found"));
        }

        Result updateResult = showcase.Update(
            command.Title,
            command.Description,
            command.Category,
            command.DemoUrl,
            command.GitHubUrl,
            command.VideoUrl,
            command.Tags,
            command.DisplayOrder,
            command.IsPublished);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await repository.UpdateAsync(showcase, cancellationToken);

        return Result.Success();
    }
}
