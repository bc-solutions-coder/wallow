using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Wallow.Tests.Common.Factories;
using WireMock.Server;

namespace Wallow.Identity.IntegrationTests.Logout;

/// <summary>
/// Hosts a WireMock relying party and permits its loopback address for delivery tests.
/// Wide delivery budgets reduce timeout interference with request-count assertions;
/// <see cref="BackchannelLogoutSsrfTests"/> covers the default loopback refusal.
/// <see cref="SlowRelyingPartyBackchannelLogoutTestFactory"/> uses short budgets separately.
/// </summary>
public class BackchannelLogoutTestFactory : WallowApiFactory
{
    private WireMockServer? _wireMock;

    public WireMockServer WireMock => _wireMock
        ?? throw new InvalidOperationException("Factory not initialized");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        _wireMock = WireMockServer.Start();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:BackchannelLogout:AllowPrivateNetworkHosts"] = "true",
                ["Identity:BackchannelLogout:PerClientTimeout"] = "00:00:30",
                ["Identity:BackchannelLogout:RetryDelay"] = "00:00:00.100",
                ["Identity:BackchannelLogout:OverallTimeout"] = "00:01:00",
            });
        });
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        _wireMock?.Stop();
        _wireMock?.Dispose();
    }
}

[CollectionDefinition(BackchannelLogoutTestCollection.Name)]
public class BackchannelLogoutTestCollection : ICollectionFixture<BackchannelLogoutTestFactory>
{
    public const string Name = "BackchannelLogout";
}

/// <summary>
/// Uses one second per attempt and five seconds overall in a separate test collection.
/// </summary>
public class SlowRelyingPartyBackchannelLogoutTestFactory : BackchannelLogoutTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Later configuration sources win: these override the generous base budgets.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:BackchannelLogout:PerClientTimeout"] = "00:00:01",
                ["Identity:BackchannelLogout:OverallTimeout"] = "00:00:05",
            });
        });
    }
}

[CollectionDefinition(SlowRelyingPartyBackchannelLogoutTestCollection.Name)]
public class SlowRelyingPartyBackchannelLogoutTestCollection
    : ICollectionFixture<SlowRelyingPartyBackchannelLogoutTestFactory>
{
    public const string Name = "SlowRelyingPartyBackchannelLogout";
}
