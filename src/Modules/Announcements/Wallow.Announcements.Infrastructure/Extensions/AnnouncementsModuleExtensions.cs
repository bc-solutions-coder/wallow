using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Application.Announcements.Services;
using Wallow.Announcements.Application.Changelogs.Interfaces;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.Announcements.Infrastructure.Persistence.Repositories;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Announcements.Infrastructure.Extensions;

public static partial class AnnouncementsModuleExtensions
{
    public static IServiceCollection AddAnnouncementsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<AnnouncementsDbContext>((sp, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsql =>
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

        services.AddTenantAwareScopedContext<AnnouncementsDbContext>();

        services.AddReadDbContext<AnnouncementsDbContext>(configuration);

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
