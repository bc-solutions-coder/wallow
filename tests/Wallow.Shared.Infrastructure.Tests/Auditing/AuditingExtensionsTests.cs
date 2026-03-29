using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wallow.Shared.Infrastructure.Core.Auditing;

namespace Wallow.Shared.Infrastructure.Tests.Auditing;

public class AuditingExtensionsTests
{
    private static IConfiguration CreateConfiguration(string connectionString = "Host=localhost;Database=test") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

    [Fact]
    public void AddWallowAuditing_RegistersAuditDbContext()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddWallowAuditing(configuration);

        services.Should().Contain(sd => sd.ServiceType == typeof(DbContextOptions<AuditDbContext>));
    }

    [Fact]
    public void AddWallowAuditing_RegistersAuditInterceptor()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddWallowAuditing(configuration);

        services.Should().ContainSingle(sd => sd.ServiceType == typeof(AuditInterceptor));
    }

    [Fact]
    public void AddWallowAuditing_RegistersAuditInterceptorAsSingleton()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddWallowAuditing(configuration);

        ServiceDescriptor descriptor = services.Single(sd => sd.ServiceType == typeof(AuditInterceptor));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddWallowAuditing_ReturnsServiceCollection()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        IServiceCollection result = services.AddWallowAuditing(configuration);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddWallowAuditing_ConfiguresNpgsqlProvider()
    {
        ServiceCollection services = new();
        string connectionString = "Host=testhost;Database=testdb;Username=user;Password=pass";
        IConfiguration configuration = CreateConfiguration(connectionString);

        services.AddWallowAuditing(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        AuditDbContext dbContext = provider.GetRequiredService<AuditDbContext>();

        dbContext.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    [Fact]
    public void AddWallowAuditing_RegistersAuditDbContextAsService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddWallowAuditing(configuration);

        services.Should().Contain(sd => sd.ServiceType == typeof(AuditDbContext));
    }

    [Fact]
    public void AddWallowAuditing_RegistersAuditDbContextAsScopedLifetime()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddWallowAuditing(configuration);

        ServiceDescriptor descriptor = services.Last(sd => sd.ServiceType == typeof(AuditDbContext));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddWallowAuditing_InterceptorResolvesFromProvider()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddWallowAuditing(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        AuditInterceptor interceptor = provider.GetRequiredService<AuditInterceptor>();

        interceptor.Should().NotBeNull();
    }

    [Fact]
    public void AddWallowAuditing_InterceptorIsSameInstanceAcrossResolutions()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddWallowAuditing(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        AuditInterceptor first = provider.GetRequiredService<AuditInterceptor>();
        AuditInterceptor second = provider.GetRequiredService<AuditInterceptor>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddWallowAuditing_WithNullConnectionString_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = null
            })
            .Build();

        Action act = () => services.AddWallowAuditing(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Connection string*not configured*");
    }

    [Fact]
    public void AddWallowAuditing_WithMissingConnectionString_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        Action act = () => services.AddWallowAuditing(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Connection string*not configured*");
    }

    [Fact]
    public void AddWallowAuditing_DbContextResolvesFromProvider()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddWallowAuditing(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        AuditDbContext dbContext = provider.GetRequiredService<AuditDbContext>();

        dbContext.Should().NotBeNull();
    }

    [Fact]
    public void AddWallowAuditing_ConfiguresMigrationsHistoryTableInAuditSchema()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddWallowAuditing(configuration);
        ServiceProvider provider = services.BuildServiceProvider();

        AuditDbContext dbContext = provider.GetRequiredService<AuditDbContext>();

        dbContext.Database.ProviderName.Should().NotBeNullOrEmpty();
        dbContext.Model.GetDefaultSchema().Should().Be("audit");
    }
}

[Trait("Category", "Integration")]
public class AuditingExtensionsIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithCleanUp(true)
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task InitializeAuditingAsync_RunsMigrationsSuccessfully()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString()
        });
        builder.Services.AddWallowAuditing(builder.Configuration);
        WebApplication app = builder.Build();

        Func<Task> act = () => app.InitializeAuditingAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAuditingAsync_CreatesAuditSchema()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString()
        });
        builder.Services.AddWallowAuditing(builder.Configuration);
        WebApplication app = builder.Build();

        await app.InitializeAuditingAsync();

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        AuditDbContext db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        bool canConnect = await db.Database.CanConnectAsync();
        canConnect.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAuditingAsync_MigrationsAreIdempotent()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString()
        });
        builder.Services.AddWallowAuditing(builder.Configuration);
        WebApplication app = builder.Build();

        await app.InitializeAuditingAsync();
        Func<Task> act = () => app.InitializeAuditingAsync();

        await act.Should().NotThrowAsync();
    }
}
