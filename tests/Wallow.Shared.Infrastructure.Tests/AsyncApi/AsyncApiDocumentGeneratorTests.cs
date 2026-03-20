using System.Text.Json.Nodes;
using Wallow.Shared.Infrastructure.Workflows.AsyncApi;

namespace Wallow.Shared.Infrastructure.Tests.AsyncApi;

public class AsyncApiDocumentGeneratorTests
{
    private sealed record TestOrderPlaced
    {
        // ReSharper disable once UnusedMember.Local
        public required Guid OrderId { get; init; }
        // ReSharper disable once UnusedMember.Local
        public required decimal Total { get; init; }
    }

    private sealed record TestPaymentReceived
    {
        // ReSharper disable once UnusedMember.Local
        public required Guid PaymentId { get; init; }
        // ReSharper disable once UnusedMember.Local
        public required string Currency { get; init; }
    }

    private static EventFlowInfo CreateFlow(
        Type eventType,
        string sourceModule,
        List<ConsumerInfo>? consumers = null) =>
        new(
            EventType: eventType,
            EventTypeName: eventType.Name,
            SourceModule: sourceModule,
            ExchangeName: $"Wallow.Shared.Contracts.{sourceModule}.{eventType.Name}",
            Consumers: consumers ?? [],
            SagaTrigger: false);

    [Fact]
    public void GenerateDocument_HasAsyncApiVersion()
    {
        AsyncApiDocumentGenerator generator = new([]);

        JsonObject doc = generator.GenerateDocument();

        doc["asyncapi"]!.GetValue<string>().Should().Be("3.0.0");
    }

    [Fact]
    public void GenerateDocument_HasInfoBlock()
    {
        AsyncApiDocumentGenerator generator = new([]);

        JsonObject doc = generator.GenerateDocument();

        JsonObject info = doc["info"]!.AsObject();
        info["title"]!.GetValue<string>().Should().Be("Wallow Event Catalog");
        info.ContainsKey("version").Should().BeTrue();
    }

    [Fact]
    public void GenerateDocument_CreatesChannelPerEvent()
    {
        EventFlowInfo flow1 = CreateFlow(typeof(TestOrderPlaced), "Sales");
        EventFlowInfo flow2 = CreateFlow(typeof(TestPaymentReceived), "Billing");
        AsyncApiDocumentGenerator generator = new([flow1, flow2]);

        JsonObject doc = generator.GenerateDocument();

        JsonObject channels = doc["channels"]!.AsObject();
        channels.Count.Should().Be(2);
        channels.ContainsKey("TestOrderPlaced").Should().BeTrue();
        channels.ContainsKey("TestPaymentReceived").Should().BeTrue();
    }

    [Fact]
    public void GenerateDocument_ChannelHasCorrectAddress()
    {
        EventFlowInfo flow = CreateFlow(typeof(TestOrderPlaced), "Sales");
        AsyncApiDocumentGenerator generator = new([flow]);

        JsonObject doc = generator.GenerateDocument();

        JsonObject channel = doc["channels"]!["TestOrderPlaced"]!.AsObject();
        channel["address"]!.GetValue<string>().Should().Be("Wallow.Shared.Contracts.Sales.TestOrderPlaced");
    }

    [Fact]
    public void GenerateDocument_ChannelMessageReferencesSchema()
    {
        EventFlowInfo flow = CreateFlow(typeof(TestOrderPlaced), "Sales");
        AsyncApiDocumentGenerator generator = new([flow]);

        JsonObject doc = generator.GenerateDocument();

        JsonObject messages = doc["channels"]!["TestOrderPlaced"]!["messages"]!.AsObject();
        messages["TestOrderPlaced"]!["$ref"]!.GetValue<string>()
            .Should().Be("#/components/schemas/TestOrderPlaced");
    }

    [Fact]
    public void GenerateDocument_CreatesPublishOperation()
    {
        EventFlowInfo flow = CreateFlow(typeof(TestOrderPlaced), "Sales");
        AsyncApiDocumentGenerator generator = new([flow]);

        JsonObject doc = generator.GenerateDocument();

        JsonObject operations = doc["operations"]!.AsObject();
        string publishKey = "Sales.publish.TestOrderPlaced";
        operations.ContainsKey(publishKey).Should().BeTrue();

        JsonObject publishOp = operations[publishKey]!.AsObject();
        publishOp["action"]!.GetValue<string>().Should().Be("send");
        publishOp["channel"]!["$ref"]!.GetValue<string>().Should().Be("#/channels/TestOrderPlaced");
        publishOp["summary"]!.GetValue<string>().Should().Contain("Sales").And.Contain("TestOrderPlaced");
    }

    [Fact]
    public void GenerateDocument_CreatesSubscribeOperationPerConsumer()
    {
        List<ConsumerInfo> consumers =
        [
            new("Billing", "OrderPlacedHandler", "HandleAsync", IsSaga: false),
            new("Communications", "OrderNotifier", "Handle", IsSaga: false)
        ];
        EventFlowInfo flow = CreateFlow(typeof(TestOrderPlaced), "Sales", consumers);
        AsyncApiDocumentGenerator generator = new([flow]);

        JsonObject doc = generator.GenerateDocument();

        JsonObject operations = doc["operations"]!.AsObject();
        operations.ContainsKey("Billing.subscribe.TestOrderPlaced").Should().BeTrue();
        operations.ContainsKey("Communications.subscribe.TestOrderPlaced").Should().BeTrue();

        JsonObject billingOp = operations["Billing.subscribe.TestOrderPlaced"]!.AsObject();
        billingOp["action"]!.GetValue<string>().Should().Be("receive");
        billingOp["channel"]!["$ref"]!.GetValue<string>().Should().Be("#/channels/TestOrderPlaced");
    }

    [Fact]
    public void GenerateDocument_SagaConsumerUsesSagaPrefix()
    {
        List<ConsumerInfo> consumers =
        [
            new("Billing", "PaymentSaga", "Start", IsSaga: true)
        ];
        EventFlowInfo flow = CreateFlow(typeof(TestOrderPlaced), "Sales", consumers);
        AsyncApiDocumentGenerator generator = new([flow]);

        JsonObject doc = generator.GenerateDocument();

        JsonObject operations = doc["operations"]!.AsObject();
        operations.ContainsKey("Billing.saga.TestOrderPlaced").Should().BeTrue();
    }

    [Fact]
    public void GenerateDocument_GeneratesComponentSchemas()
    {
        EventFlowInfo flow1 = CreateFlow(typeof(TestOrderPlaced), "Sales");
        EventFlowInfo flow2 = CreateFlow(typeof(TestPaymentReceived), "Billing");
        AsyncApiDocumentGenerator generator = new([flow1, flow2]);

        JsonObject doc = generator.GenerateDocument();

        JsonObject schemas = doc["components"]!["schemas"]!.AsObject();
        schemas.Count.Should().Be(2);

        JsonObject orderSchema = schemas["TestOrderPlaced"]!.AsObject();
        orderSchema["type"]!.GetValue<string>().Should().Be("object");
        orderSchema["properties"]!.AsObject().ContainsKey("orderId").Should().BeTrue();
        orderSchema["properties"]!.AsObject().ContainsKey("total").Should().BeTrue();

        JsonObject paymentSchema = schemas["TestPaymentReceived"]!.AsObject();
        paymentSchema["properties"]!.AsObject().ContainsKey("paymentId").Should().BeTrue();
        paymentSchema["properties"]!.AsObject().ContainsKey("currency").Should().BeTrue();
    }

    [Fact]
    public void GenerateDocument_CachesResult()
    {
        EventFlowInfo flow = CreateFlow(typeof(TestOrderPlaced), "Sales");
        AsyncApiDocumentGenerator generator = new([flow]);

        JsonObject first = generator.GenerateDocument();
        JsonObject second = generator.GenerateDocument();

        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void GenerateDocument_EmptyFlows_ProducesValidDocument()
    {
        AsyncApiDocumentGenerator generator = new([]);

        JsonObject doc = generator.GenerateDocument();

        doc["asyncapi"]!.GetValue<string>().Should().Be("3.0.0");
        doc["channels"]!.AsObject().Count.Should().Be(0);
        doc["operations"]!.AsObject().Count.Should().Be(0);
        doc["components"]!["schemas"]!.AsObject().Count.Should().Be(0);
    }
}
