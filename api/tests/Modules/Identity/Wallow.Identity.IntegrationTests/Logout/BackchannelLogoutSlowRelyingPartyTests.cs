using System.Diagnostics;
using Wallow.Identity.IntegrationTests.OAuth2;
using WireMock.ResponseBuilders;

namespace Wallow.Identity.IntegrationTests.Logout;

/// <summary>
/// Checks that slow delivery does not wait for the relying party response.
/// A separate collection isolates its short budgets from
/// <see cref="BackchannelLogoutNotificationTests"/>.
/// </summary>
[Collection(SlowRelyingPartyBackchannelLogoutTestCollection.Name)]
[Trait("Category", "Integration")]
public sealed class BackchannelLogoutSlowRelyingPartyTests(
    SlowRelyingPartyBackchannelLogoutTestFactory factory)
    : BackchannelLogoutDeliveryTestBase(factory)
{
    [Fact]
    public async Task Logout_StaysBoundedWhenTheRelyingPartyIsSlow()
    {
        // The 20-second response exceeds the delivery budgets; allow 10 seconds for logout overhead.
        Seed seed = await SeedAsync(rpBehaviour: rp => rp.RespondWith(
            Response.Create().WithStatusCode(200).WithDelay(TimeSpan.FromSeconds(20))));
        using AuthorizationCodeFlowHarness harness = await SignedInWithTokensAsync(seed);

        Stopwatch stopwatch = Stopwatch.StartNew();
        await LogoutAsync(harness);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }
}
