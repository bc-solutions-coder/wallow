using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wallow.Storage.Application.Extensions;
using Wallow.Storage.Infrastructure.Persistence;

namespace Wallow.Storage.Infrastructure.Extensions;

public static partial class StorageModuleExtensions
{
    public static IServiceCollection AddStorageModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddStorageApplication();
        services.AddStorageInfrastructure(configuration);
        return services;
    }

    public static async Task<WebApplication> InitializeStorageModuleAsync(
        this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            try
            {
                await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
                StorageDbContext db = scope.ServiceProvider.GetRequiredService<StorageDbContext>();
                await db.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("StorageModule");
                LogStartupFailed(logger, ex);
            }
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Storage module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
