using Testcontainers.Redis;

namespace Wallow.Tests.Common.Fixtures;

public class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("valkey/valkey:8-alpine")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync() => await _redis.StartAsync();

    // Dispose the container after each test run to prevent container accumulation.
    public async Task DisposeAsync() => await _redis.DisposeAsync();
}

[CollectionDefinition(nameof(RedisTestCollection))]
public class RedisTestCollection : ICollectionFixture<RedisFixture>
{
}
