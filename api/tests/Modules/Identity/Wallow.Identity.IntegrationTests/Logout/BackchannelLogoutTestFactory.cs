using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Wallow.Tests.Common.Factories;
using WireMock.Server;

namespace Wallow.Identity.IntegrationTests.Logout;

/// <summary>
/// Factory for back-channel logout delivery tests: hosts a WireMock relying party and tunes
/// <c>Identity:BackchannelLogout</c> for test speed. WireMock answers on 127.0.0.1, which the
/// SSRF gate refuses by default, so delivery tests opt in via AllowPrivateNetworkHosts — the
/// refusal itself is covered by <see cref="BackchannelLogoutSsrfTests"/> against the default
/// configuration.
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
                ["Identity:BackchannelLogout:PerClientTimeout"] = "00:00:01",
                ["Identity:BackchannelLogout:RetryDelay"] = "00:00:00.100",
                ["Identity:BackchannelLogout:OverallTimeout"] = "00:00:05",
            });
        });
    }

    public new async Task DisposeAsync()
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
