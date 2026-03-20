using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wallow.Shared.Infrastructure.Core.Auditing;

public static partial class AuditingExtensions
{
    public static IServiceCollection AddWallowAuditing(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuditDbContext>((_, options) =>
        {
            string? connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit");
            });
        });

        services.AddLogging();
        services.AddSingleton<AuditInterceptor>();

        return services;
    }

    public static async Task InitializeAuditingAsync(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            try
            {
                await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
                AuditDbContext db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
                await db.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Auditing");
                LogMigrationFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Audit database migration failed. Ensure PostgreSQL is running.")]
    private static partial void LogMigrationFailed(ILogger logger, Exception ex);
}
