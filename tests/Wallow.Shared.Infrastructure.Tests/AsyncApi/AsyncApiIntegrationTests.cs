using System.Text.Json.Nodes;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Infrastructure.Workflows.AsyncApi;

namespace Wallow.Shared.Infrastructure.Tests.AsyncApi;

[Trait("Category", "Integration")]
public class AsyncApiIntegrationTests
{
    private static IReadOnlyList<EventFlowInfo> DiscoverFromContracts()
    {
        EventFlowDiscovery discovery = new();
        return discovery.Discover([typeof(UserRegisteredEvent).Assembly]);
    }

    [Fact]
    public void Discover_FindsRealEventsFromContractsAssembly()
    {
        IReadOnlyList<EventFlowInfo> flows = DiscoverFromContracts();

        flows.Should().NotBeEmpty();
        flows.Should().Contain(f => f.EventTypeName == nameof(UserRegisteredEvent));
    }

    [Fact]
    public void Discover_ExtractsIdentityModuleForUserRegisteredEvent()
    {
        IReadOnlyList<EventFlowInfo> flows = DiscoverFromContracts();

        EventFlowInfo userEvent = flows.Single(f => f.EventTypeName == nameof(UserRegisteredEvent));
        userEvent.SourceModule.Should().Be("Identity");
    }

    [Fact]
    public void Discover_FindsEventsFromMultipleModules()
    {
        IReadOnlyList<EventFlowInfo> flows = DiscoverFromContracts();

        List<string> modules = flows.Select(f => f.SourceModule).Distinct().ToList();
        modules.Should().Contain("Identity");
        modules.Should().Contain("Billing");
        modules.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void AsyncApiDocument_FromRealDiscovery_HasValidStructure()
    {
        IReadOnlyList<EventFlowInfo> flows = DiscoverFromContracts();
        AsyncApiDocumentGenerator generator = new(flows.ToArray());

        JsonObject doc = generator.GenerateDocument();

        doc["asyncapi"]!.GetValue<string>().Should().Be("3.0.0");
        doc["info"]!.AsObject().ContainsKey("title").Should().BeTrue();
        doc["channels"]!.AsObject().Count.Should().BeGreaterThan(0);
        doc["operations"]!.AsObject().Count.Should().BeGreaterThan(0);
        doc["components"]!["schemas"]!.AsObject().Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AsyncApiDocument_FromRealDiscovery_ContainsUserRegisteredEventChannel()
    {
        IReadOnlyList<EventFlowInfo> flows = DiscoverFromContracts();
        AsyncApiDocumentGenerator generator = new(flows.ToArray());

        JsonObject doc = generator.GenerateDocument();

        JsonObject channels = doc["channels"]!.AsObject();
        channels.ContainsKey(nameof(UserRegisteredEvent)).Should().BeTrue();
    }

    [Fact]
    public void AsyncApiDocument_FromRealDiscovery_SchemasMatchDiscoveredFlows()
    {
        IReadOnlyList<EventFlowInfo> flows = DiscoverFromContracts();
        AsyncApiDocumentGenerator generator = new(flows.ToArray());

        JsonObject doc = generator.GenerateDocument();

        JsonObject schemas = doc["components"]!["schemas"]!.AsObject();
        List<string> distinctNames = flows.Select(f => f.EventTypeName).Distinct().ToList();
        schemas.Count.Should().Be(distinctNames.Count);

        foreach (string name in distinctNames)
        {
            schemas.ContainsKey(name).Should().BeTrue($"schema for {name} should exist");
        }
    }

    [Fact]
    public void MermaidDiagram_FromRealDiscovery_ContainsExpectedModules()
    {
        IReadOnlyList<EventFlowInfo> flows = DiscoverFromContracts();

        string mermaid = MermaidFlowGenerator.Generate(flows);

        mermaid.Should().StartWith("flowchart LR");
        mermaid.Should().Contain("Identity");
        mermaid.Should().Contain("Billing");
        mermaid.Should().Contain("RabbitMQ");
    }

    [Fact]
    public void FullPipeline_DiscoverThenGenerateBoth_ProducesConsistentOutput()
    {
        IReadOnlyList<EventFlowInfo> flows = DiscoverFromContracts();
        AsyncApiDocumentGenerator generator = new(flows.ToArray());
        JsonObject doc = generator.GenerateDocument();
        string mermaid = MermaidFlowGenerator.Generate(flows);

        // Every event in the AsyncAPI doc should also appear in the Mermaid diagram's source modules
        JsonObject operations = doc["operations"]!.AsObject();
        List<string> publishOps = operations
            .Select(kvp => kvp.Key)
            .Where(k => k.Contains(".publish."))
            .ToList();

        publishOps.Should().NotBeEmpty();

        // Mermaid should mention every source module found in flows
        foreach (string module in flows.Select(f => f.SourceModule).Distinct())
        {
            mermaid.Should().Contain(module, $"Mermaid diagram should reference module {module}");
        }
    }
}
