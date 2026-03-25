using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Wallow.Shared.Infrastructure.Core.Extensions;

namespace Wallow.Shared.Infrastructure.Core.Auditing;

public static partial class AuditingExtensions
{
    public static IServiceCollection AddWallowAuditing(
        this IServiceCollection services, IConfiguration configuration)
    {
        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<AuditDbContext>((_, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit");
            });
        });

        services.AddScoped<AuditDbContext>(sp =>
        {
            IDbContextFactory<AuditDbContext> factory = sp.GetRequiredService<IDbContextFactory<AuditDbContext>>();
            return factory.CreateDbContext();
        });

        services.AddReadDbContext<AuditDbContext>(configuration);

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
