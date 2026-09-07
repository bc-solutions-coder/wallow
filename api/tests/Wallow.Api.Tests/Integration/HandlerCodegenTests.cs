using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Tests.Common.Factories;
using Wolverine.Configuration;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Resolves every discovered handler chain to exercise Wolverine code generation without dispatching messages.
/// AllChains includes separated endpoint chains; each must be resolved against its assigned endpoint.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class HandlerCodegenTests(WallowApiFactory factory)
{
    [Fact]
    public void EveryDiscoveredHandler_Compiles()
    {
        using IServiceScope scope = factory.Services.CreateScope();

        // Access handler lookup through the concrete runtime.
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
                // Resolve separated handlers against their assigned queue.
                Endpoint? endpoint = chain.Endpoints.Count > 0 ? chain.Endpoints[0] : null;

                IMessageHandler? handler = endpoint is null
                    ? runtime.Handlers.HandlerFor(chain.MessageType)
                    : runtime.Handlers.HandlerFor(chain.MessageType, endpoint);

                handler.Should().NotBeNull();
            }
#pragma warning disable CA1031 // Collect compilation failures so the assertion reports them together.
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
