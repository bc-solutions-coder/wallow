using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Announcements.Application.Announcements.Commands.CreateAnnouncement;
using Wallow.Announcements.Infrastructure.Extensions;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.Announcements.Infrastructure.Modules;

public sealed class AnnouncementsModule : IWallowModule
{
    public string Name => "Announcements";

    public bool IsCore => false;

    public IReadOnlyList<Assembly> HandlerAssemblies =>
    [
        typeof(CreateAnnouncementHandler).Assembly,
        typeof(AnnouncementsModule).Assembly,
    ];

    public IReadOnlyList<Type> DbContextTypes => [typeof(AnnouncementsDbContext)];

    public string SchemaName => "announcements";

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddAnnouncementsModule(configuration);
    }
}
