using Foundry.Billing.Application.EventHandlers;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Events;
using Foundry.Billing.Domain.Identity;
using Foundry.Shared.Contracts.Billing.Events;
using Microsoft.Extensions.Logging;
using NSubstitute.Core;
using Wolverine;
using static Foundry.Tests.Common.Helpers.LoggerAssertionExtensions;

namespace Foundry.Billing.Tests.Application.EventHandlers;

public class InvoiceCreatedDomainEventHandlerTests
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<InvoiceCreatedDomainEventHandler> _logger;

    public InvoiceCreatedDomainEventHandlerTests()
    {
        _invoiceRepository = Substitute.For<IInvoiceRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _logger = Substitute.For<ILogger<InvoiceCreatedDomainEventHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task HandleAsync_PublishesIntegrationEvent()
    {
        Guid invoiceId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime dueDate = DateTime.UtcNow.AddDays(30);

        Invoice invoice = Invoice.Create(userId, "INV-001", "USD", userId, TimeProvider.System, dueDate);

        InvoiceCreatedDomainEvent domainEvent = new InvoiceCreatedDomainEvent(
            invoiceId,
            userId,
            100m,
            "USD");

        _invoiceRepository.GetByIdAsync(InvoiceId.Create(invoiceId), Arg.Any<CancellationToken>())
            .Returns(invoice);

        await InvoiceCreatedDomainEventHandler.HandleAsync(
            domainEvent,
            _invoiceRepository,
            _messageBus,
            _logger,
            CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<InvoiceCreatedEvent>(e =>
                e.InvoiceId == invoiceId &&
                e.UserId == userId &&
                e.InvoiceNumber == "INV-001" &&
                e.Amount == 100m &&
                e.Currency == "USD" &&
                e.DueDate == dueDate));
    }

    [Fact]
    public async Task HandleAsync_RetrievesInvoiceFromRepository()
    {
        Guid invoiceId = Guid.NewGuid();
        InvoiceCreatedDomainEvent domainEvent = new InvoiceCreatedDomainEvent(
            invoiceId,
            Guid.NewGuid(),
            100m,
            "USD");

        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, DateTime.UtcNow.AddDays(30));

        _invoiceRepository.GetByIdAsync(InvoiceId.Create(invoiceId), Arg.Any<CancellationToken>())
            .Returns(invoice);

        await InvoiceCreatedDomainEventHandler.HandleAsync(
            domainEvent,
            _invoiceRepository,
            _messageBus,
            _logger,
            CancellationToken.None);

        await _invoiceRepository.Received(1).GetByIdAsync(
            Arg.Is<InvoiceId>(id => id.Value == invoiceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationToken()
    {
        Guid invoiceId = Guid.NewGuid();
        InvoiceCreatedDomainEvent domainEvent = new InvoiceCreatedDomainEvent(
            invoiceId,
            Guid.NewGuid(),
            100m,
            "USD");

        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, DateTime.UtcNow.AddDays(30));

        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        using CancellationTokenSource cts = new CancellationTokenSource();

        await InvoiceCreatedDomainEventHandler.HandleAsync(
            domainEvent,
            _invoiceRepository,
            _messageBus,
            _logger,
            cts.Token);

        await _invoiceRepository.Received(1).GetByIdAsync(
            Arg.Any<InvoiceId>(),
            cts.Token);
    }

    [Fact]
    public async Task HandleAsync_LogsInformation()
    {
        Guid invoiceId = Guid.NewGuid();
        InvoiceCreatedDomainEvent domainEvent = new InvoiceCreatedDomainEvent(
            invoiceId,
            Guid.NewGuid(),
            100m,
            "USD");

        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, DateTime.UtcNow.AddDays(30));

        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        await InvoiceCreatedDomainEventHandler.HandleAsync(
            domainEvent,
            _invoiceRepository,
            _messageBus,
            _logger,
            CancellationToken.None);

        List<ICall> calls = _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Where(c => c.GetArguments()[0] is LogLevel level && level == LogLevel.Information)
            .Where(c => LogMessageContains(c, invoiceId.ToString()))
            .ToList();
        calls.Should().HaveCountGreaterThanOrEqualTo(1, "expected at least one Information log call containing the invoice ID");
    }

    [Fact]
    public async Task HandleAsync_PublishesEventEvenWhenInvoiceIsNull()
    {
        Guid invoiceId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        InvoiceCreatedDomainEvent domainEvent = new InvoiceCreatedDomainEvent(
            invoiceId,
            userId,
            100m,
            "USD");

        _invoiceRepository.GetByIdAsync(InvoiceId.Create(invoiceId), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        await InvoiceCreatedDomainEventHandler.HandleAsync(
            domainEvent,
            _invoiceRepository,
            _messageBus,
            _logger,
            CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<InvoiceCreatedEvent>(e =>
                e.InvoiceId == invoiceId &&
                e.UserId == userId &&
                e.Amount == 100m &&
                e.Currency == "USD"));
    }
}
