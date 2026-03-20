using Wallow.Notifications.Application.Preferences.DTOs;
using Wallow.Notifications.Application.Preferences.Interfaces;
using Wallow.Notifications.Domain.Preferences.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Preferences.Queries;

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
