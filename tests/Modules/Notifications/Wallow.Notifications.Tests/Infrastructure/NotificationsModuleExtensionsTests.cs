using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Application.Channels.Sms.Interfaces;
using Wallow.Notifications.Application.Preferences.Interfaces;
using Wallow.Notifications.Infrastructure.Extensions;
using Wallow.Notifications.Infrastructure.Jobs;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Notifications.Infrastructure.Services;
using Wallow.Shared.Contracts.Communications.Email;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;

namespace Wallow.Notifications.Tests.Infrastructure;

public class NotificationsModuleExtensionsTests
{
    private static IConfiguration CreateConfiguration(
        Dictionary<string, string?>? overrides = null)
    {
        Dictionary<string, string?> defaults = new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;",
            ["Smtp:Host"] = "localhost",
            ["Smtp:Port"] = "1025",
            ["Smtp:DefaultFromAddress"] = "test@wallow.local",
            ["Smtp:DefaultFromName"] = "Wallow Test",
            ["Notifications:Email:Provider"] = "Smtp"
        };

        if (overrides is not null)
        {
            foreach (KeyValuePair<string, string?> kvp in overrides)
            {
                defaults[kvp.Key] = kvp.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();
    }

    [Fact]
    public void AddNotificationsModule_RegistersDbContext()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(NotificationsDbContext));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_RegistersEmailProvider()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IEmailProvider));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<SmtpEmailProvider>();
    }

    [Fact]
    public void AddNotificationsModule_RegistersSmtpConnectionPool()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(SmtpConnectionPool));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddNotificationsModule_RegistersEmailService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IEmailService));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_RegistersEmailTemplateService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IEmailTemplateService));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_RegistersNotificationService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(INotificationService));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_RegistersPreferenceChecker()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(INotificationPreferenceChecker));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_WithTwilioConfig_RegistersTwilioSmsProvider()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["TwilioSettings:AccountSid"] = "AC_test_sid",
            ["TwilioSettings:AuthToken"] = "test_token",
            ["TwilioSettings:FromNumber"] = "+15551234567"
        });

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ISmsProvider));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<TwilioSmsProvider>();
    }

    [Fact]
    public void AddNotificationsModule_WithoutTwilioConfig_RegistersNullSmsProvider()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ISmsProvider));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<NullSmsProvider>();
    }

    [Fact]
    public void AddNotificationsModule_RegistersPushProviderFactory()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IPushProviderFactory));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_RegistersPushCredentialEncryptor()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IPushCredentialEncryptor));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddNotificationsModule_RegistersEmailMessageRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IEmailMessageRepository));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddNotificationsModule_RegistersNotificationRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(INotificationRepository));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_RegistersSmsMessageRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ISmsMessageRepository));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_RegistersChannelPreferenceRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IChannelPreferenceRepository));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_WithUnrecognizedProvider_DefaultsToSmtp()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Notifications:Email:Provider"] = "UnknownProvider"
        });

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? emailProvider = services.FirstOrDefault(
            d => d.ServiceType == typeof(IEmailProvider));
        emailProvider.Should().NotBeNull();
        emailProvider!.ImplementationType.Should().Be<SmtpEmailProvider>();
    }

    [Fact]
    public void AddNotificationsModule_WithNullProvider_DefaultsToSmtp()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Notifications:Email:Provider"] = null
        });

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? emailProvider = services.FirstOrDefault(
            d => d.ServiceType == typeof(IEmailProvider));
        emailProvider.Should().NotBeNull();
        emailProvider!.ImplementationType.Should().Be<SmtpEmailProvider>();
    }

    [Fact]
    public void AddNotificationsModule_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        IServiceCollection result = services.AddNotificationsModule(configuration);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddNotificationsModule_RegistersDeviceRegistrationRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IDeviceRegistrationRepository));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_RegistersPushMessageRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IPushMessageRepository));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_RegistersEmailPreferenceRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IEmailPreferenceRepository));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_RegistersTenantPushConfigurationRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ITenantPushConfigurationRepository));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_RegistersRetryFailedEmailsJob()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(RetryFailedEmailsJob));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddNotificationsModule_RegistersSmtpResiliencePipeline()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        ResiliencePipelineProvider<string> pipelineProvider =
            provider.GetRequiredService<ResiliencePipelineProvider<string>>();
        ResiliencePipeline pipeline = pipelineProvider.GetPipeline("smtp");

        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void AddNotificationsModule_ConfiguresSmtpSettings()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "mail.example.com",
            ["Smtp:Port"] = "587"
        });

        services.AddNotificationsModule(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        Microsoft.Extensions.Options.IOptions<SmtpSettings> options =
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SmtpSettings>>();

        options.Value.Host.Should().Be("mail.example.com");
        options.Value.Port.Should().Be(587);
    }

    [Fact]
    public void AddNotificationsModule_ConfiguresTwilioSettings()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["TwilioSettings:AccountSid"] = "AC_test",
            ["TwilioSettings:AuthToken"] = "token123",
            ["TwilioSettings:FromNumber"] = "+15551234567"
        });

        services.AddNotificationsModule(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        Microsoft.Extensions.Options.IOptions<TwilioSettings> options =
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TwilioSettings>>();

        options.Value.AccountSid.Should().Be("AC_test");
    }

    [Fact]
    public void AddNotificationsModule_ConfiguresPushSettings()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["PushSettings:Enabled"] = "true"
        });

        services.AddNotificationsModule(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        Microsoft.Extensions.Options.IOptions<PushSettings> options =
            provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<PushSettings>>();

        options.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task InitializeNotificationsModuleAsync_WhenDbUnavailable_LogsWarningAndReturnsApp()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=1;Database=nonexistent;Timeout=1"
        });
        builder.Services.AddScoped<ITenantContext>(_ => Substitute.For<ITenantContext>());
        builder.Services.AddSingleton(new TenantSaveChangesInterceptor());
        builder.Services.AddScoped<IRealtimeDispatcher>(_ => Substitute.For<IRealtimeDispatcher>());
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHttpClient();
        builder.Services.AddNotificationsModule(builder.Configuration);
        builder.Logging.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace);
        WebApplication app = builder.Build();

        WebApplication result = await app.InitializeNotificationsModuleAsync();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public void AddNotificationsModule_RegistersDataProtection()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(Microsoft.AspNetCore.DataProtection.IDataProtectionProvider));
        descriptor.Should().NotBeNull();
    }
}
