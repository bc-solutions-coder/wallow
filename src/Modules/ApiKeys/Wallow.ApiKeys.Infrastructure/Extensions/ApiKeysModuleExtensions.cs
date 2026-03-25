using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wallow.ApiKeys.Infrastructure.Persistence;

namespace Wallow.ApiKeys.Infrastructure.Extensions;

public static partial class ApiKeysModuleExtensions
{
    public static IServiceCollection AddApiKeysModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApiKeysInfrastructure(configuration);
        return services;
    }

    public static async Task<WebApplication> InitializeApiKeysModuleAsync(
        this WebApplication app)
    {
        try
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            ApiKeysDbContext db = scope.ServiceProvider.GetRequiredService<ApiKeysDbContext>();
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                await db.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("ApiKeysModule");
            LogStartupFailed(logger, ex);
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "ApiKeys module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
