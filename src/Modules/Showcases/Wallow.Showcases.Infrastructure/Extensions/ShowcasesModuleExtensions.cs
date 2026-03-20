using Wallow.Showcases.Application.Contracts;
using Wallow.Showcases.Application.Extensions;
using Wallow.Showcases.Infrastructure.Persistence;
using Wallow.Showcases.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wallow.Showcases.Infrastructure.Extensions;

public static partial class ShowcasesModuleExtensions
{
    public static IServiceCollection AddShowcasesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddShowcasesApplication();
        services.AddShowcasesPersistence(configuration);
        return services;
    }

    private static IServiceCollection AddShowcasesPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ShowcasesDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "showcases");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
        });

        services.AddScoped<IShowcaseRepository, ShowcaseRepository>();

        return services;
    }

    public static async Task InitializeShowcasesModuleAsync(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            try
            {
                await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
                ShowcasesDbContext db = scope.ServiceProvider.GetRequiredService<ShowcasesDbContext>();
                await db.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ShowcasesModule");
                LogStartupFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Showcases module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
