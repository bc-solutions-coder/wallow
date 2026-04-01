using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wallow.Shared.Infrastructure.Core.Extensions;

namespace Wallow.Shared.Infrastructure.Core.Auditing;

public static class AuditingExtensions
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

#pragma warning disable IDE0060, RCS1175 // Extension method kept for API consistency with other modules
    public static Task InitializeAppAuditingAsync(this WebApplication app)
    {
        return Task.CompletedTask;
    }
#pragma warning restore IDE0060, RCS1175
}
