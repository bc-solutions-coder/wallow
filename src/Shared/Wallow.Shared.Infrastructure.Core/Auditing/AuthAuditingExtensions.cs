using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Kernel.Auditing;

namespace Wallow.Shared.Infrastructure.Core.Auditing;

public static partial class AuthAuditingExtensions
{
    public static IServiceCollection AddAuthAuditing(
        this IServiceCollection services, IConfiguration configuration)
    {
        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<AuthAuditDbContext>((_, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "auth_audit");
            });
        });

        services.AddScoped<AuthAuditDbContext>(sp =>
        {
            IDbContextFactory<AuthAuditDbContext> factory = sp.GetRequiredService<IDbContextFactory<AuthAuditDbContext>>();
            return factory.CreateDbContext();
        });

        services.AddReadDbContext<AuthAuditDbContext>(configuration);

        services.AddScoped<IAuthAuditService, AuthAuditService>();

        return services;
    }

    public static async Task InitializeAuthAuditingAsync(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            try
            {
                await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
                AuthAuditDbContext db = scope.ServiceProvider.GetRequiredService<AuthAuditDbContext>();
                await db.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("AuthAuditing");
                LogMigrationFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Auth audit database migration failed. Ensure PostgreSQL is running.")]
    private static partial void LogMigrationFailed(ILogger logger, Exception ex);
}
