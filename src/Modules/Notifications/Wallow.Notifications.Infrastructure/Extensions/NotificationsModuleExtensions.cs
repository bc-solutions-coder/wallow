using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Application.Channels.Sms.Interfaces;
using Wallow.Notifications.Application.Extensions;
using Wallow.Notifications.Application.Preferences.Interfaces;
using Wallow.Notifications.Infrastructure.Jobs;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Notifications.Infrastructure.Persistence.Repositories;
using Wallow.Notifications.Infrastructure.Services;
using Wallow.Shared.Contracts.Communications.Email;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Infrastructure.Core.Resilience;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Wallow.Notifications.Infrastructure.Extensions;

public static partial class NotificationsModuleExtensions
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddNotificationsApplication();
        services
            .AddNotificationsPersistence(configuration)
            .AddNotificationsServices(configuration);
        services.AddReadDbContext<NotificationsDbContext>(configuration);

        return services;
    }

    private static IServiceCollection AddNotificationsPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<NotificationsDbContext>((sp, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications");
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

        services.AddScoped<NotificationsDbContext>(sp =>
        {
            IDbContextFactory<NotificationsDbContext> factory = sp.GetRequiredService<IDbContextFactory<NotificationsDbContext>>();
            NotificationsDbContext ctx = factory.CreateDbContext();
            ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
            ctx.SetTenant(tenant.TenantId);
            return ctx;
        });

        // Email repositories
        services.AddScoped<IEmailMessageRepository, EmailMessageRepository>();
        services.AddScoped<IEmailPreferenceRepository, EmailPreferenceRepository>();

        // InApp notification repositories
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // SMS repositories
        services.AddScoped<ISmsMessageRepository, SmsMessageRepository>();

        // Channel preference repositories
        services.AddScoped<IChannelPreferenceRepository, ChannelPreferenceRepository>();

        // Push repositories
        services.AddScoped<IDeviceRegistrationRepository, DeviceRegistrationRepository>();
        services.AddScoped<ITenantPushConfigurationRepository, TenantPushConfigurationRepository>();
        services.AddScoped<IPushMessageRepository, PushMessageRepository>();

        return services;
    }

    private static IServiceCollection AddNotificationsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // SMTP resilience pipeline
        services.AddResiliencePipeline("smtp", builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromSeconds(2)
                })
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(30)
                });
        });

        // Email services
        services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
        RegisterEmailProvider(services, configuration);
        services.AddScoped<IEmailService, EmailProviderAdapter>();
        services.AddScoped<IEmailTemplateService, SimpleEmailTemplateService>();

        // InApp notification services
        services.AddScoped<INotificationService, SignalRNotificationService>();

        // Preference checking
        services.AddScoped<INotificationPreferenceChecker, NotificationPreferenceChecker>();

        // Background jobs
        services.AddScoped<RetryFailedEmailsJob>();

        // SMS services
        services.Configure<TwilioSettings>(configuration.GetSection("TwilioSettings"));

        string? twilioAccountSid = configuration["TwilioSettings:AccountSid"];
        if (!string.IsNullOrEmpty(twilioAccountSid))
        {
            services.AddHttpClient<TwilioSmsProvider>()
                .AddWallowResilienceHandler("external-api");
            services.AddScoped<ISmsProvider, TwilioSmsProvider>();
        }
        else
        {
            services.AddScoped<ISmsProvider, NullSmsProvider>();
        }

        // Push services
        services.AddDataProtection();
        services.Configure<PushSettings>(configuration.GetSection("PushSettings"));
        services.AddSingleton<IPushCredentialEncryptor, PushCredentialEncryptor>();
        services.AddScoped<IPushProviderFactory, PushProviderFactory>();

        return services;
    }

    private static void RegisterEmailProvider(IServiceCollection services, IConfiguration configuration)
    {
        string provider = configuration.GetValue<string>("Notifications:Email:Provider") ?? "Smtp";

        switch (provider)
        {
            case "Smtp":
                services.AddSingleton<SmtpConnectionPool>();
                services.AddScoped<IEmailProvider, SmtpEmailProvider>();
                break;
            default:
                Console.WriteLine($"Warning: Unrecognized email provider '{provider}'. Defaulting to Smtp.");
                services.AddSingleton<SmtpConnectionPool>();
                services.AddScoped<IEmailProvider, SmtpEmailProvider>();
                break;
        }
    }

    public static async Task<WebApplication> InitializeNotificationsModuleAsync(
        this WebApplication app)
    {
        try
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            NotificationsDbContext db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                await db.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("NotificationsModule");
            LogStartupFailed(logger, ex);
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notifications module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
