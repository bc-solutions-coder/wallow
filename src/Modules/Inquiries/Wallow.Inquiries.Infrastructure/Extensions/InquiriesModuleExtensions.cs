using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wallow.Inquiries.Application.Extensions;
using Wallow.Inquiries.Infrastructure.Persistence;

namespace Wallow.Inquiries.Infrastructure.Extensions;

public static partial class InquiriesModuleExtensions
{
    public static IServiceCollection AddInquiriesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddInquiriesApplication();
        services.AddInquiriesInfrastructure(configuration);
        return services;
    }

    public static async Task<WebApplication> InitializeInquiriesModuleAsync(
        this WebApplication app)
    {
        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("InquiriesModule");
        try
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            InquiriesDbContext db = scope.ServiceProvider.GetRequiredService<InquiriesDbContext>();
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Inquiries module database migrations applied")]
    private static partial void LogMigrationsApplied(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inquiries module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
