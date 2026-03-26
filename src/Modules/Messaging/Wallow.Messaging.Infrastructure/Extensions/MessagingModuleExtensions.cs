using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Infrastructure.Persistence;
using Wallow.Messaging.Infrastructure.Persistence.Repositories;
using Wallow.Messaging.Infrastructure.Services;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Messaging.Infrastructure.Extensions;

public static partial class MessagingModuleExtensions
{
    public static IServiceCollection AddMessagingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<MessagingDbContext>((sp, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "messaging");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddTenantAwareScopedContext<MessagingDbContext>();

        services.AddReadDbContext<MessagingDbContext>(configuration);

        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessagingQueryService, EfMessagingQueryService>();

        return services;
    }

    public static async Task<WebApplication> InitializeMessagingModuleAsync(
        this WebApplication app)
    {
        try
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            MessagingDbContext db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                await db.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("MessagingModule");
            LogStartupFailed(logger, ex);
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Messaging module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
