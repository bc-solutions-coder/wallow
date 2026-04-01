using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Application.Announcements.Services;
using Wallow.Announcements.Application.Changelogs.Interfaces;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.Announcements.Infrastructure.Persistence.Repositories;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Announcements.Infrastructure.Extensions;

public static class AnnouncementsModuleExtensions
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

    public static Task<WebApplication> InitializeAnnouncementsModuleAsync(
        this WebApplication app)
    {
        return Task.FromResult(app);
    }
}
