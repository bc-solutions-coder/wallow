using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Tests.Common.Factories;
using Wolverine.Configuration;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Compiles every discovered Wolverine handler, so a dependency the codegen cannot inline-construct
/// fails here rather than in production.
/// <para>
/// <c>ServiceLocationPolicy.NotAllowed</c> is evaluated when Wolverine compiles a handler, and under
/// <c>TypeLoadMode.Dynamic</c> that happens on the first message of that type — not at startup, and
/// not in a unit test that news the handler up itself. So the policy violation surfaces inside a
/// background envelope, three retries later, in the dead-letter queue, behind an HTTP 200. That is
/// how a single constructor parameter on SendEmailHandler took out every transactional email in the
/// product — verification, magic links, OTP, password reset, invitations, access requests — and was
/// found by four browser specs rather than by the .NET suite.
/// </para>
/// <para>
/// <see cref="HandlerGraph.HandlerFor(Type, Endpoint)"/> runs that compilation without dispatching
/// anything, so this covers all discovered handlers rather than the ones some test happens to send.
/// Nothing else in the suite compiles a handler: handler unit tests construct the class directly and
/// registration tests assert only that a ServiceDescriptor exists. Neither touches the generated
/// adapter.
/// </para>
/// <para>
/// Iterate <see cref="HandlerGraph.AllChains"/>, never <see cref="HandlerGraph.Chains"/>. Under
/// <c>MultipleHandlerBehavior.Separated</c> a message type with more than one handler keeps a
/// top-level chain that holds no handlers at all — they have moved into per-endpoint sticky
/// sub-chains under <see cref="HandlerChain.ByEndpoint"/>, each listening on its own
/// <c>local://</c> queue. <c>Chains</c> yields only those empty parents, so the four multi-handler
/// message types would be walked but never compiled, and the single-argument
/// <c>HandlerFor(messageType)</c> would throw <c>NoHandlerForEndpointException</c> on them.
/// <c>AllChains()</c> drops the placeholder parents and yields the sticky sub-chains instead, and
/// the endpoint-aware overload resolves each one against the queue it actually listens on.
/// </para>
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class HandlerCodegenTests(WallowApiFactory factory)
{
    [Fact]
    public void EveryDiscoveredHandler_Compiles()
    {
        using IServiceScope scope = factory.Services.CreateScope();

        // HandlerGraph hangs off the concrete runtime; IWolverineRuntime does not expose it.
        WolverineRuntime runtime = (WolverineRuntime)scope.ServiceProvider.GetRequiredService<IWolverineRuntime>();

        HandlerChain[] chains = [.. runtime.Handlers.AllChains()];

        chains.Should().NotBeEmpty(
            "handler discovery walks the loaded Wallow.* assemblies, so an empty graph means this " +
            "test is asserting nothing rather than that the codegen is clean");

        chains.Length.Should().BeGreaterThanOrEqualTo(
            runtime.Handlers.Chains.Length,
            "AllChains() replaces each separated message type's placeholder parent with one " +
            "sub-chain per handler, so it can never cover fewer handlers than Chains does");

        StringBuilder failures = new();

        foreach (HandlerChain chain in chains)
        {
            try
            {
                // A sticky sub-chain is only reachable through the queue it was assigned to;
                // asking for the message type alone would resolve the empty parent. Chains that
                // were never assigned an endpoint fall through to the plain lookup.
                Endpoint? endpoint = chain.Endpoints.Count > 0 ? chain.Endpoints[0] : null;

                IMessageHandler? handler = endpoint is null
                    ? runtime.Handlers.HandlerFor(chain.MessageType)
                    : runtime.Handlers.HandlerFor(chain.MessageType, endpoint);

                handler.Should().NotBeNull();
            }
#pragma warning disable CA1031 // every compilation failure is reported together, not just the first
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failures.Append(
                    CultureInfo.InvariantCulture,
                    $"\n- {chain.MessageType.FullName} ({chain.TypeName}): {ex.Message}");
            }
        }

        failures.Length.Should().Be(
            0,
            $"every handler must compile under ServiceLocationPolicy.NotAllowed, but:{failures}");
    }
}
