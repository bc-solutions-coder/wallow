using Foundry.Shared.Kernel.Results;
using Foundry.Showcases.Application.Contracts;
using Foundry.Showcases.Domain.Entities;
using Foundry.Showcases.Domain.Identity;

namespace Foundry.Showcases.Application.Commands.DeleteShowcase;

public sealed record DeleteShowcaseCommand(ShowcaseId Id);

public sealed class DeleteShowcaseHandler(IShowcaseRepository repository)
{
    public async Task<Result> Handle(
        DeleteShowcaseCommand command,
        CancellationToken cancellationToken)
    {
        Showcase? showcase = await repository.GetByIdAsync(command.Id, cancellationToken);

        if (showcase is null)
        {
            return Result.Failure(Error.NotFound("Showcase.NotFound",
                $"Showcase with ID '{command.Id.Value}' was not found"));
        }

        await repository.DeleteAsync(command.Id, cancellationToken);

        return Result.Success();
    }
}
