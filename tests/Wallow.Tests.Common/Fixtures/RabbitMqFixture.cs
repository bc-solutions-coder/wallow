using Testcontainers.RabbitMq;

namespace Wallow.Tests.Common.Fixtures;

public class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4.2-management-alpine")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _rabbitMq.GetConnectionString();

    public async Task InitializeAsync() => await _rabbitMq.StartAsync();

    // Dispose the container after each test run to prevent container accumulation.
    public async Task DisposeAsync() => await _rabbitMq.DisposeAsync();
}
