using Wallow.ApiKeys.Infrastructure.Modules;
using Wallow.Branding.Infrastructure.Modules;
using Wallow.Identity.Infrastructure.Modules;
using Wallow.Inquiries.Infrastructure.Modules;
using Wallow.Notifications.Infrastructure.Modules;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Storage.Infrastructure.Modules;

namespace Wallow.Modules.Registry;

/// <summary>
/// Shipped modules shared by API registration and migrations. The API filters optional modules;
/// the migration host takes the full list.
/// </summary>
public static class WallowModuleRegistry
{
    /// <summary>
    /// Modules in registration order, with core Identity services first.
    /// </summary>
    public static IReadOnlyList<IWallowModule> All { get; } =
    [
        new IdentityModule(),
        new BrandingModule(),
        new NotificationsModule(),
        new StorageModule(),
        new ApiKeysModule(),
        new InquiriesModule(),
    ];
}
