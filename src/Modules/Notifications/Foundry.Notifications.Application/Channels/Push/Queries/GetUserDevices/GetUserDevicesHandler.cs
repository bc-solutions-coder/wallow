using Foundry.Notifications.Application.Channels.Push.DTOs;
using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Application.Channels.Push.Queries.GetUserDevices;

public sealed class GetUserDevicesHandler(IDeviceRegistrationRepository deviceRegistrationRepository)
{
    public async Task<Result<IReadOnlyList<DeviceRegistrationDto>>> Handle(
        GetUserDevicesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DeviceRegistration> registrations = await deviceRegistrationRepository.GetActiveByUserAsync(
            new UserId(query.UserId),
            cancellationToken);

        List<DeviceRegistrationDto> dtos = registrations.Select(r => new DeviceRegistrationDto(
            r.Id.Value,
            r.UserId.Value,
            r.Platform,
            r.Token,
            r.IsActive,
            r.RegisteredAt)).ToList();

        return Result.Success<IReadOnlyList<DeviceRegistrationDto>>(dtos);
    }
}
