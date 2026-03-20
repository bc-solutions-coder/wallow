using Wallow.Identity.Application.Extensions;
using Wallow.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wallow.Identity.Infrastructure.Extensions;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static partial class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddIdentityApplication();
        services.AddIdentityInfrastructure(configuration, environment);
        return services;
    }

    public static async Task<WebApplication> InitializeIdentityModuleAsync(
        this WebApplication app)
    {
        try
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                await db.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("IdentityModule");
            LogStartupFailed(logger, ex);
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Identity module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
