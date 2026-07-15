using Testcontainers.PostgreSql;

namespace Wallow.Tests.Common.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("wallow_test")
        .WithUsername("test")
        .WithPassword("test")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    // Dispose the container after each test run to prevent container accumulation.
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}
