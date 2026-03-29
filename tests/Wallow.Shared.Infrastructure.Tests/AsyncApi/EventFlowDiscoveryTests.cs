using System.Reflection;
using Wallow.Shared.Contracts.Billing.Events;
using Wallow.Shared.Contracts.TestSales.Events;
using Wallow.Shared.Handlers;
using Wallow.Shared.Infrastructure.Tests.AsyncApi.Stubs;
using Wallow.Shared.Infrastructure.Workflows.AsyncApi;
using Wallow.TestBilling.Application.Sagas;
using Wallow.TestBilling.Infrastructure.Consumers;

namespace Wallow.Shared.Infrastructure.Tests.AsyncApi
{
    public class EventFlowDiscoveryTests
    {
        private readonly EventFlowDiscovery _sut = new();

        [Fact]
        public void Discover_FindsEventsFromAssembly()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            flows.Should().Contain(f => f.EventTypeName == nameof(TestOrderPlacedEvent));
            flows.Should().Contain(f => f.EventTypeName == nameof(TestInvoicePaidEvent));
        }

        [Fact]
        public void Discover_ExtractsCorrectSourceModuleFromContractsNamespace()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo orderFlow = flows.Single(f => f.EventTypeName == nameof(TestOrderPlacedEvent));
            orderFlow.SourceModule.Should().Be("TestSales");

            EventFlowInfo invoiceFlow = flows.Single(f => f.EventTypeName == nameof(TestInvoicePaidEvent));
            invoiceFlow.SourceModule.Should().Be("Billing");
        }

        [Fact]
        public void Discover_FindsConsumersFromHandlerMethods()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo orderFlow = flows.Single(f => f.EventTypeName == nameof(TestOrderPlacedEvent));
            orderFlow.Consumers.Should().Contain(c =>
                c.HandlerTypeName == nameof(TestOrderPlacedHandler) && c.HandlerMethodName == "Handle" && !c.IsSaga);
        }

        [Fact]
        public void Discover_FindsConsumersFromSagaStartMethods()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo orderFlow = flows.Single(f => f.EventTypeName == nameof(TestOrderPlacedEvent));
            orderFlow.Consumers.Should().Contain(c =>
                c.HandlerTypeName == nameof(TestOrderSaga) && c.HandlerMethodName == "Start" && c.IsSaga);
            orderFlow.SagaTrigger.Should().BeTrue();
        }

        [Fact]
        public void Discover_ReturnsEmptyConsumers_WhenNoHandlersExist()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo orphanFlow = flows.Single(f => f.EventTypeName == nameof(TestOrphanEvent));
            orphanFlow.Consumers.Should().BeEmpty();
        }

        [Fact]
        public void Discover_HandlesAssembliesWithNoEvents()
        {
            Assembly coreLib = typeof(object).Assembly;

            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([coreLib]);

            flows.Should().BeEmpty();
        }

        [Fact]
        public void Discover_ExtractsHandlerModuleFromNamespace()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo orderFlow = flows.Single(f => f.EventTypeName == nameof(TestOrderPlacedEvent));
            ConsumerInfo handler = orderFlow.Consumers.First(c => c.HandlerTypeName == nameof(TestOrderPlacedHandler));
            handler.Module.Should().Be("TestBilling");
        }

        [Fact]
        public void Discover_SetsExchangeNameToFullTypeName()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo orderFlow = flows.Single(f => f.EventTypeName == nameof(TestOrderPlacedEvent));
            orderFlow.ExchangeName.Should().Be(typeof(TestOrderPlacedEvent).FullName);
        }

        [Fact]
        public void Discover_ResultsAreOrderedByModuleThenEventName()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            List<string> modules = flows.Select(f => f.SourceModule).ToList();
            modules.Should().BeInAscendingOrder();
        }

        [Fact]
        public void Discover_HandlesReflectionTypeLoadException_Gracefully()
        {
            Assembly faultyAssembly = new ReflectionTypeLoadAssemblyStub();

            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([faultyAssembly]);

            flows.Should().Contain(f => f.EventTypeName == nameof(TestOrderPlacedEvent));
        }

        [Fact]
        public void Discover_HandlerInSharedNamespace_ExtractsSharedModule()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo orderFlow = flows.Single(f => f.EventTypeName == nameof(TestOrderPlacedEvent));
            orderFlow.Consumers.Should().Contain(c =>
                c.HandlerTypeName == nameof(TestSharedOrderHandler) && c.Module == "Shared");
        }

        [Fact]
        public void Discover_SagaInstanceHandleMethods_AreDiscovered()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo invoiceFlow = flows.Single(f => f.EventTypeName == nameof(TestInvoicePaidEvent));
            invoiceFlow.Consumers.Should().Contain(c =>
                c.HandlerTypeName == nameof(TestOrderSaga) && c.HandlerMethodName == "Handle" && c.IsSaga);
        }

        [Fact]
        public void Discover_SagaInstanceHandleAsyncMethods_AreDiscovered()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo invoiceFlow = flows.Single(f => f.EventTypeName == nameof(TestInvoicePaidEvent));
            invoiceFlow.Consumers.Should().Contain(c =>
                c.HandlerTypeName == nameof(TestOrderSaga) && c.HandlerMethodName == "HandleAsync" && c.IsSaga);
        }

        [Fact]
        public void Discover_EmptyAssemblyList_ReturnsEmpty()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([]);

            flows.Should().BeEmpty();
        }

        [Fact]
        public void Discover_EventWithFullName_SetsExchangeNameToFullName()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo flow = flows.Single(f => f.EventTypeName == nameof(TestInvoicePaidEvent));
            flow.ExchangeName.Should().Be(typeof(TestInvoicePaidEvent).FullName);
        }

        [Fact]
        public void Discover_EventNotTriggeredBySaga_HasSagaTriggerFalse()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo orphanFlow = flows.Single(f => f.EventTypeName == nameof(TestOrphanEvent));
            orphanFlow.SagaTrigger.Should().BeFalse();
        }

        [Fact]
        public void EventFlowInfo_ConsumerModules_ReturnsDistinctModules()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo orderFlow = flows.Single(f => f.EventTypeName == nameof(TestOrderPlacedEvent));
            IReadOnlyList<string> modules = orderFlow.ConsumerModules;
            modules.Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void Discover_HandlerWithNoMatchingEventParam_IsIgnored()
        {
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo orphanFlow = flows.Single(f => f.EventTypeName == nameof(TestOrphanEvent));
            orphanFlow.Consumers.Should().BeEmpty();
        }

        [Fact]
        public void Discover_InvoicePaidEvent_HasSagaTriggerFalse()
        {
            // InvoicePaidEvent is handled by saga's Handle (not Start), so it should NOT be a saga trigger
            IReadOnlyList<EventFlowInfo> flows = _sut.Discover([typeof(TestOrderPlacedEvent).Assembly]);

            EventFlowInfo invoiceFlow = flows.Single(f => f.EventTypeName == nameof(TestInvoicePaidEvent));
            invoiceFlow.SagaTrigger.Should().BeFalse();
        }
    }
}

// --- Fake event types in Contracts-style namespaces ---

namespace Wallow.Shared.Contracts.TestSales.Events
{
    public record TestOrderPlacedEvent : IntegrationEvent;
}

namespace Wallow.Shared.Contracts.Billing.Events
{
    public record TestInvoicePaidEvent : IntegrationEvent;
    public record TestOrphanEvent : IntegrationEvent;
}

// --- Fake handler in a module namespace ---

namespace Wallow.TestBilling.Infrastructure.Consumers
{
    public static class TestOrderPlacedHandler
    {
        public static void Handle(
            TestOrderPlacedEvent _)
        { }
    }
}

// --- Fake saga (needs a base class named "Saga" to be detected) ---

namespace Wallow.TestBilling.Application.Sagas
{
    public abstract class Saga { }

    public class TestOrderSaga : Saga
    {
        public static void Start(
            TestOrderPlacedEvent _)
        { }

        public void Handle(
            TestInvoicePaidEvent _)
        { }

        public Task HandleAsync(
            TestInvoicePaidEvent _) => Task.CompletedTask;
    }
}

// --- Fake handler in Shared namespace ---

namespace Wallow.Shared.Handlers
{
    public static class TestSharedOrderHandler
    {
        public static void Handle(
            TestOrderPlacedEvent _)
        { }
    }
}

// --- Assembly stub that throws ReflectionTypeLoadException ---

namespace Wallow.Shared.Infrastructure.Tests.AsyncApi.Stubs
{
    public class ReflectionTypeLoadAssemblyStub : Assembly
    {
        public override Type[] GetTypes()
        {
            Type[] loadedTypes = [typeof(TestOrderPlacedEvent), null!];
            throw new ReflectionTypeLoadException(loadedTypes, [new InvalidOperationException("Simulated load failure")]);
        }
    }
}
