using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Wallow.Tests.Common.Factories;
using WireMock.Server;

namespace Wallow.Identity.IntegrationTests.Logout;

/// <summary>
/// Factory for back-channel logout delivery tests: hosts a WireMock relying party and tunes
/// <c>Identity:BackchannelLogout</c>. WireMock answers on 127.0.0.1, which the SSRF gate
/// refuses by default, so delivery tests opt in via AllowPrivateNetworkHosts — the refusal
/// itself is covered by <see cref="BackchannelLogoutSsrfTests"/> against the default
/// configuration.
/// </summary>
/// <remarks>
/// The delivery budgets are deliberately generous. The tests against this factory assert EXACT
/// request counts at the relying party, and a per-attempt timeout that fires under suite load
/// cancels an attempt before it ever reaches WireMock — shaving the observed count and making
/// the assertion flaky. With budgets no loaded run can plausibly exhaust, every attempt runs to
/// its real HTTP outcome and the counts depend only on the relying party's scripted behaviour.
/// (A true fake clock cannot pin this: nothing could advance it while the real HTTP round-trip
/// is in flight, so wide margins are the strongest determinism available here — the exact
/// retry contract itself is pinned under a fake clock in the notifier's unit suite.)
/// Only the retry pause stays short:
/// nothing asserts on it, it just costs wall-clock time in the failing-relying-party tests.
/// The one test that needs tight budgets — a slow relying party must not hold sign-out
/// hostage — runs against <see cref="SlowRelyingPartyBackchannelLogoutTestFactory"/> instead.
/// </remarks>
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
/// Tight-budget variant for the slow-relying-party bound test: one second per delivery attempt,
/// five seconds overall. Its own factory and collection keep these budgets away from the
/// exact-count delivery tests, where a short per-attempt timeout is exactly what made them
/// flaky on a loaded machine.
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
