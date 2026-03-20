using Wallow.Shared.Infrastructure.Workflows.AsyncApi;

namespace Wallow.Shared.Infrastructure.Tests.AsyncApi;

public class MermaidFlowGeneratorTests
{
    [Fact]
    public void Generate_EmptyFlows_ReturnsFlowchartHeader()
    {
        string result = MermaidFlowGenerator.Generate([]);

        result.Should().Be("flowchart LR");
    }

    [Fact]
    public void Generate_SingleFlow_StartsWithFlowchartLR()
    {
        List<EventFlowInfo> flows = [CreateFlow("Billing", "InvoiceCreated")];

        string result = MermaidFlowGenerator.Generate(flows);

        result.Should().StartWith("flowchart LR");
    }

    [Fact]
    public void Generate_SingleFlow_ProducerConnectsToExchange()
    {
        List<EventFlowInfo> flows = [CreateFlow("Billing", "InvoiceCreated")];

        string result = MermaidFlowGenerator.Generate(flows);

        result.Should().Contain("Billing -->|\"InvoiceCreated\"| exchange");
    }

    [Fact]
    public void Generate_FlowWithConsumer_ExchangeConnectsToConsumer()
    {
        List<EventFlowInfo> flows =
        [
            CreateFlow("Sales", "OrderPlaced", consumers:
            [
                new ConsumerInfo("Inventory", "OrderPlacedHandler", "Handle", IsSaga: false)
            ])
        ];

        string result = MermaidFlowGenerator.Generate(flows);

        result.Should().Contain("exchange -->|\"OrderPlaced\"| Inventory_cons[\"Inventory\"]");
    }

    [Fact]
    public void Generate_MultipleConsumers_ProducesMultipleEdges()
    {
        List<EventFlowInfo> flows =
        [
            CreateFlow("Sales", "OrderPlaced", consumers:
            [
                new ConsumerInfo("Inventory", "OrderPlacedHandler", "Handle", IsSaga: false),
                new ConsumerInfo("Billing", "OrderPlacedHandler", "Handle", IsSaga: false)
            ])
        ];

        string result = MermaidFlowGenerator.Generate(flows);

        result.Should().Contain("exchange -->|\"OrderPlaced\"| Inventory_cons[\"Inventory\"]");
        result.Should().Contain("exchange -->|\"OrderPlaced\"| Billing_cons[\"Billing\"]");
    }

    [Fact]
    public void Generate_NoConsumers_StillShowsProducerToExchangeEdge()
    {
        List<EventFlowInfo> flows = [CreateFlow("Billing", "InvoicePaid")];

        string result = MermaidFlowGenerator.Generate(flows);

        result.Should().Contain("Billing -->|\"InvoicePaid\"| exchange");
        result.Should().NotContain("exchange -->|\"InvoicePaid\"|");
    }

    [Fact]
    public void Generate_SagaTrigger_AddsLightningEmoji()
    {
        List<EventFlowInfo> flows =
        [
            CreateFlow("Sales", "OrderPlaced", sagaTrigger: true, consumers:
            [
                new ConsumerInfo("Inventory", "OrderSaga", "Start", IsSaga: true)
            ])
        ];

        string result = MermaidFlowGenerator.Generate(flows);

        result.Should().Contain("OrderPlaced ⚡");
    }

    [Fact]
    public void Generate_ContainsExchangeNode()
    {
        List<EventFlowInfo> flows = [CreateFlow("Billing", "InvoiceCreated")];

        string result = MermaidFlowGenerator.Generate(flows);

        result.Should().Contain("exchange{{RabbitMQ}}");
    }

    [Fact]
    public void Generate_ContainsProducerSubgraph()
    {
        List<EventFlowInfo> flows = [CreateFlow("Billing", "InvoiceCreated")];

        string result = MermaidFlowGenerator.Generate(flows);

        result.Should().Contain("subgraph Billing_sub[\"Billing\"]");
        result.Should().Contain("Billing([\"Billing\"])");
    }

    private static EventFlowInfo CreateFlow(
        string sourceModule,
        string eventName,
        bool sagaTrigger = false,
        List<ConsumerInfo>? consumers = null)
    {
        return new EventFlowInfo(
            EventType: typeof(object),
            EventTypeName: eventName,
            SourceModule: sourceModule,
            ExchangeName: $"Wallow.Shared.Contracts.{sourceModule}.Events.{eventName}",
            Consumers: consumers ?? [],
            SagaTrigger: sagaTrigger);
    }
}
