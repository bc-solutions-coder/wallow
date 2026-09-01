using System.Diagnostics;
using Wallow.Identity.IntegrationTests.OAuth2;
using WireMock.ResponseBuilders;

namespace Wallow.Identity.IntegrationTests.Logout;

/// <summary>
/// A slow relying party cannot hold the user's sign-out hostage. This lives in its own
/// collection because it is the one delivery test that needs the tight per-attempt and overall
/// budgets its factory configures — the exact-count tests in
/// <see cref="BackchannelLogoutNotificationTests"/> run with generous budgets precisely so those
/// timeouts cannot fire under load and cancel attempts they are counting.
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
        // The RP answers far outside the 1s per-attempt budget; two timed-out attempts plus the
        // retry pause put the budget-bounded worst case near 2s. The asserted bound sits well
        // above that so a loaded run cannot flake it, and still far enough below the RP's 20s
        // answer to prove logout never waited for it.
        Seed seed = await SeedAsync(rpBehaviour: rp => rp.RespondWith(
            Response.Create().WithStatusCode(200).WithDelay(TimeSpan.FromSeconds(20))));
        using AuthorizationCodeFlowHarness harness = await SignedInWithTokensAsync(seed);

        Stopwatch stopwatch = Stopwatch.StartNew();
        await LogoutAsync(harness);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }
}
