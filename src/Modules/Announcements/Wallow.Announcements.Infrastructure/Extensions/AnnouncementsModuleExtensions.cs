using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Application.Announcements.Services;
using Wallow.Announcements.Application.Changelogs.Interfaces;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.Announcements.Infrastructure.Persistence.Repositories;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wallow.Announcements.Infrastructure.Extensions;

public static partial class AnnouncementsModuleExtensions
{
    public static IServiceCollection AddAnnouncementsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration;

        services.AddDbContext<AnnouncementsDbContext>((sp, options) =>
        {
            string? connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "announcements");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        // Repositories
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<IAnnouncementDismissalRepository, AnnouncementDismissalRepository>();
        services.AddScoped<IChangelogRepository, ChangelogRepository>();

        // Services
        services.AddScoped<IAnnouncementTargetingService, AnnouncementTargetingService>();

        return services;
    }

    public static async Task<WebApplication> InitializeAnnouncementsModuleAsync(
        this WebApplication app)
    {
        try
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            AnnouncementsDbContext db = scope.ServiceProvider.GetRequiredService<AnnouncementsDbContext>();
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                await db.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("AnnouncementsModule");
            LogStartupFailed(logger, ex);
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Announcements module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
