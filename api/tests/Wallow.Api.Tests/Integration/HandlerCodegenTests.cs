using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Tests.Common.Factories;
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
/// <see cref="HandlerGraph.HandlerFor(Type)"/> runs that compilation without dispatching anything, so
/// this covers all discovered handlers rather than the ones some test happens to send. Nothing else
/// in the suite compiles a handler: handler unit tests construct the class directly and registration
/// tests assert only that a ServiceDescriptor exists. Neither touches the generated adapter.
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

        HandlerChain[] chains = [.. runtime.Handlers.Chains];

        chains.Should().NotBeEmpty(
            "handler discovery walks the loaded Wallow.* assemblies, so an empty graph means this " +
            "test is asserting nothing rather than that the codegen is clean");

        StringBuilder failures = new();

        foreach (HandlerChain chain in chains)
        {
            try
            {
                runtime.Handlers.HandlerFor(chain.MessageType).Should().NotBeNull();
            }
#pragma warning disable CA1031 // every compilation failure is reported together, not just the first
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failures.Append(CultureInfo.InvariantCulture, $"\n- {chain.MessageType.FullName}: {ex.Message}");
            }
        }

        failures.Length.Should().Be(
            0,
            $"every handler must compile under ServiceLocationPolicy.NotAllowed, but:{failures}");
    }
}
