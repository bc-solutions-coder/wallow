using Wallow.Announcements.Infrastructure.Modules;
using Wallow.ApiKeys.Infrastructure.Modules;
using Wallow.Branding.Infrastructure.Modules;
using Wallow.Identity.Infrastructure.Modules;
using Wallow.Inquiries.Infrastructure.Modules;
using Wallow.Notifications.Infrastructure.Modules;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Storage.Infrastructure.Modules;

namespace Wallow.Modules.Registry;

/// <summary>
/// The single list of modules the platform ships. Both hosts read it: <c>Wallow.Api</c> filters it
/// through <c>IFeatureManager</c>, and <c>Wallow.MigrationService</c> takes it unfiltered.
/// </summary>
public static class WallowModuleRegistry
{
    /// <summary>
    /// Gets every module the platform ships, in registration order. Identity is
    /// <see cref="IWallowModule.IsCore"/> and comes first because the rest of the platform assumes
    /// its schema and services are present.
    /// </summary>
    public static IReadOnlyList<IWallowModule> All { get; } =
    [
        new IdentityModule(),
        new BrandingModule(),
        new NotificationsModule(),
        new AnnouncementsModule(),
        new StorageModule(),
        new ApiKeysModule(),
        new InquiriesModule(),
    ];
}
