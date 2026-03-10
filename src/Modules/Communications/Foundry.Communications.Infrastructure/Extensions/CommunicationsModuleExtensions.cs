using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Application.Channels.Sms.Interfaces;
using Foundry.Communications.Application.Extensions;
using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Communications.Application.Preferences.Interfaces;
using Foundry.Communications.Application.Settings;
using Foundry.Communications.Infrastructure.Jobs;
using Foundry.Communications.Infrastructure.Persistence;
using Foundry.Communications.Infrastructure.Persistence.Repositories;
using Foundry.Communications.Infrastructure.Services;
using Foundry.Shared.Contracts.Communications.Email;
using Foundry.Shared.Infrastructure.Settings;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Foundry.Shared.Infrastructure.Core.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Foundry.Communications.Infrastructure.Extensions;

public static partial class CommunicationsModuleExtensions
{
    public static IServiceCollection AddCommunicationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = services.AddCommunicationsApplication();
        services
            .AddCommunicationsPersistence(configuration)
            .AddCommunicationsServices(configuration);
        services.AddSettings<CommunicationsDbContext, CommunicationsSettingKeys>();

        return services;
    }

    private static IServiceCollection AddCommunicationsPersistence(
        this IServiceCollection services,
        IConfiguration _)
    {
        services.AddDbContext<CommunicationsDbContext>((sp, options) =>
        {
            string? connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "communications");
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

        // Email repositories
        services.AddScoped<IEmailMessageRepository, EmailMessageRepository>();
        services.AddScoped<IEmailPreferenceRepository, EmailPreferenceRepository>();

        // InApp notification repositories
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // SMS repositories
        services.AddScoped<ISmsMessageRepository, SmsMessageRepository>();

        // Channel preference repositories
        services.AddScoped<IChannelPreferenceRepository, ChannelPreferenceRepository>();

        // Announcement repositories
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<IChangelogRepository, ChangelogRepository>();
        services.AddScoped<IAnnouncementDismissalRepository, AnnouncementDismissalRepository>();

        // Messaging repositories
        services.AddScoped<IConversationRepository, ConversationRepository>();

        // Messaging query services
        services.AddScoped<IMessagingQueryService, MessagingQueryService>();

        return services;
    }

    private static IServiceCollection AddCommunicationsServices(
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

        // Background jobs
        services.AddScoped<RetryFailedEmailsJob>();

        // SMS services
        services.Configure<TwilioSettings>(configuration.GetSection("TwilioSettings"));

        string? twilioAccountSid = configuration["TwilioSettings:AccountSid"];
        if (!string.IsNullOrEmpty(twilioAccountSid))
        {
            services.AddHttpClient<TwilioSmsProvider>()
                .AddFoundryResilienceHandler("external-api");
            services.AddScoped<ISmsProvider, TwilioSmsProvider>();
        }
        else
        {
            services.AddScoped<ISmsProvider, NullSmsProvider>();
        }

        return services;
    }

    private static void RegisterEmailProvider(IServiceCollection services, IConfiguration configuration)
    {
        string provider = configuration.GetValue<string>("Communications:Email:Provider") ?? "Smtp";

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

    public static async Task<WebApplication> InitializeCommunicationsModuleAsync(
        this WebApplication app)
    {
        try
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            CommunicationsDbContext db = scope.ServiceProvider.GetRequiredService<CommunicationsDbContext>();
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
            {
                await db.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("CommunicationsModule");
            LogStartupFailed(logger, ex);
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Communications module startup failed. Ensure PostgreSQL is running.")]
    private static partial void LogStartupFailed(ILogger logger, Exception ex);
}
