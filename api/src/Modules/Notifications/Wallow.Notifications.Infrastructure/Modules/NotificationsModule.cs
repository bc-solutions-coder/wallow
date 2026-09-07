using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Notifications.Infrastructure.Extensions;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.Notifications.Infrastructure.Modules;

public sealed class NotificationsModule : IWallowModule
{
    /// <summary>
    /// Schema shared by the model and migration history table.
    /// Internal to keep schema ownership within this module.
    /// </summary>
    internal const string Schema = "notifications";

    public string Name => "Notifications";

    public bool IsCore => false;

    public IReadOnlyList<Assembly> HandlerAssemblies =>
    [
        typeof(UserRoleChangedNotificationHandler).Assembly,
        typeof(NotificationsModule).Assembly,
    ];

    public IReadOnlyList<Type> DbContextTypes => [typeof(NotificationsDbContext)];

    public string SchemaName => Schema;

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddNotificationsModule(configuration);
    }
}
