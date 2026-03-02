using Foundry.Communications.Application.Preferences.DTOs;
using Foundry.Communications.Application.Preferences.Interfaces;
using Foundry.Communications.Domain.Preferences.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Preferences.Queries;

public sealed record GetChannelPreferencesQuery(Guid UserId);

public sealed class GetChannelPreferencesHandler(IChannelPreferenceRepository preferenceRepository)
{
    public async Task<Result<IReadOnlyList<ChannelPreferenceDto>>> Handle(
        GetChannelPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChannelPreference> preferences = await preferenceRepository.GetByUserIdAsync(
            query.UserId,
            cancellationToken);

        List<ChannelPreferenceDto> dtos = preferences.Select(p => new ChannelPreferenceDto(
            p.Id.Value,
            p.UserId,
            p.ChannelType,
            p.NotificationType,
            p.IsEnabled,
            p.CreatedAt,
            p.UpdatedAt)).ToList();

        return Result.Success<IReadOnlyList<ChannelPreferenceDto>>(dtos);
    }
}
