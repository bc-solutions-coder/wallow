using Wallow.Inquiries.Application.Extensions;
using Wallow.Inquiries.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
        try
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            InquiriesDbContext db = scope.ServiceProvider.GetRequiredService<InquiriesDbContext>();
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                await db.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("InquiriesModule");
            LogStartupFailed(logger, ex);
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inquiries module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
