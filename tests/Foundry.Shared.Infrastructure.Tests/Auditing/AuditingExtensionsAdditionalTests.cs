using Foundry.Shared.Infrastructure.Core.Auditing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Foundry.Shared.Infrastructure.Tests.Auditing;

public class AuditingExtensionsAdditionalTests
{
    private static IConfiguration CreateConfiguration(string connectionString = "Host=localhost;Database=test") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

    [Fact]
    public void AddFoundryAuditing_RegistersLoggingServices()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddFoundryAuditing(configuration);

        services.Should().Contain(sd => sd.ServiceType == typeof(ILoggerFactory));
    }

    [Fact]
    public void AddFoundryAuditing_CanBeCalledMultipleTimes_WithoutThrowingException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        Action act = () =>
        {
            services.AddFoundryAuditing(configuration);
            services.AddFoundryAuditing(configuration);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task InitializeAuditingAsync_InNonDevelopmentEnvironment_SkipsMigration()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=invalid;Database=nonexistent"
        });
        builder.Services.AddFoundryAuditing(builder.Configuration);
        WebApplication app = builder.Build();

        // Should not throw even with an invalid connection string because migration is skipped
        Func<Task> act = () => app.InitializeAuditingAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAuditingAsync_InStagingEnvironment_SkipsMigration()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Staging;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=invalid;Database=nonexistent"
        });
        builder.Services.AddFoundryAuditing(builder.Configuration);
        WebApplication app = builder.Build();

        Func<Task> act = () => app.InitializeAuditingAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAuditingAsync_WhenMigrationFails_DoesNotThrow()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Invalid connection string will cause migration to fail
            ["ConnectionStrings:DefaultConnection"] = "Host=invalid_host_that_does_not_exist;Database=nonexistent;Timeout=1"
        });
        builder.Services.AddFoundryAuditing(builder.Configuration);
        WebApplication app = builder.Build();

        Func<Task> act = () => app.InitializeAuditingAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAuditingAsync_WhenMigrationFails_LogsWarning()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=invalid_host_that_does_not_exist;Database=nonexistent;Timeout=1"
        });
        builder.Services.AddFoundryAuditing(builder.Configuration);

        using FakeLoggerProvider fakeLoggerProvider = new();
        builder.Services.AddSingleton<ILoggerProvider>(fakeLoggerProvider);

        WebApplication app = builder.Build();

        await app.InitializeAuditingAsync();

        fakeLoggerProvider.LogEntries.Should().Contain(entry =>
            entry.LogLevel == LogLevel.Warning &&
            entry.Message.Contains("Audit database migration failed"));
    }

    [Fact]
    public void AddFoundryAuditing_WithEmptyConnectionString_DoesNotThrowDuringRegistration()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(string.Empty);

        Action act = () => services.AddFoundryAuditing(configuration);

        act.Should().NotThrow();
    }

    private sealed class FakeLoggerProvider : ILoggerProvider
    {
        public List<LogEntry> LogEntries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new FakeLogger(this, categoryName);

        public void Dispose() { }

        public void AddEntry(LogEntry entry) => LogEntries.Add(entry);
    }

    private sealed class FakeLogger(FakeLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            provider.AddEntry(new LogEntry
            {
                CategoryName = categoryName,
                LogLevel = logLevel,
                Message = formatter(state, exception),
                Exception = exception
            });
        }
    }

    private sealed class LogEntry
    {
        public required string CategoryName { get; init; }
        public required LogLevel LogLevel { get; init; }
        public required string Message { get; init; }
        public Exception? Exception { get; init; }
    }
}
