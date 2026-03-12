namespace Foundry.Notifications.Application.Channels.Push.Queries.GetUserDevices;

public sealed record GetUserDevicesQuery(Guid UserId, Guid TenantId);
