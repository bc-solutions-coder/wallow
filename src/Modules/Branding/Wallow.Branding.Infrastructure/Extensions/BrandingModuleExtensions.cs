using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wallow.Branding.Infrastructure.Persistence;

namespace Wallow.Branding.Infrastructure.Extensions;

public static partial class BrandingModuleExtensions
{
    public static IServiceCollection AddBrandingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddBrandingInfrastructure(configuration);
        return services;
    }

    public static async Task<WebApplication> InitializeBrandingModuleAsync(
        this WebApplication app)
    {
        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("BrandingModule");
        try
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            BrandingDbContext db = scope.ServiceProvider.GetRequiredService<BrandingDbContext>();
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                await db.Database.MigrateAsync();
                LogMigrationsApplied(logger);
            }
        }
        catch (Exception ex)
        {
            LogStartupFailed(logger, ex);
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Branding module database migrations applied")]
    private static partial void LogMigrationsApplied(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Branding module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
