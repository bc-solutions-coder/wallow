using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Kernel.Auditing;

namespace Wallow.Shared.Infrastructure.Core.Auditing;

public static class AuthAuditingExtensions
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

#pragma warning disable IDE0060, RCS1175 // Extension method kept for API consistency with other modules
    public static Task InitializeAuthAuditingAsync(this WebApplication app)
    {
        return Task.CompletedTask;
    }
#pragma warning restore IDE0060, RCS1175
}
