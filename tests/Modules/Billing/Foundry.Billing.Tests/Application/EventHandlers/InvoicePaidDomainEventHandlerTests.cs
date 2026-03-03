using Foundry.Billing.Application.EventHandlers;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Events;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Contracts.Billing.Events;
using Microsoft.Extensions.Logging;
using NSubstitute.Core;
using Wolverine;
using static Foundry.Tests.Common.Helpers.LoggerAssertionExtensions;

namespace Foundry.Billing.Tests.Application.EventHandlers;

public class InvoicePaidDomainEventHandlerTests
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<InvoicePaidDomainEventHandler> _logger;

    public InvoicePaidDomainEventHandlerTests()
    {
        _invoiceRepository = Substitute.For<IInvoiceRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _logger = Substitute.For<ILogger<InvoicePaidDomainEventHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task HandleAsync_PublishesIntegrationEvent()
    {
        Guid invoiceId = Guid.NewGuid();
        Guid paymentId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime paidAt = DateTime.UtcNow;

        Invoice invoice = Invoice.Create(userId, "INV-001", "USD", userId, TimeProvider.System, DateTime.UtcNow.AddDays(30));

        invoice.AddLineItem("Test Item", Money.Create(100m, "USD"), 1, userId, TimeProvider.System);

        InvoicePaidDomainEvent domainEvent = new InvoicePaidDomainEvent(
            invoiceId,
            paymentId,
            paidAt);

        _invoiceRepository.GetByIdAsync(InvoiceId.Create(invoiceId), Arg.Any<CancellationToken>())
            .Returns(invoice);

        await InvoicePaidDomainEventHandler.HandleAsync(
            domainEvent,
            _invoiceRepository,
            _messageBus,
            _logger,
            CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<InvoicePaidEvent>(e =>
                e.InvoiceId == invoiceId &&
                e.PaymentId == paymentId &&
                e.UserId == userId &&
                e.InvoiceNumber == "INV-001" &&
                e.Amount == 100m &&
                e.Currency == "USD" &&
                e.PaidAt == paidAt));
    }

    [Fact]
    public async Task HandleAsync_WhenInvoiceNotFound_DoesNotPublish()
    {
        Guid invoiceId = Guid.NewGuid();
        InvoicePaidDomainEvent domainEvent = new InvoicePaidDomainEvent(
            invoiceId,
            Guid.NewGuid(),
            DateTime.UtcNow);

        _invoiceRepository.GetByIdAsync(InvoiceId.Create(invoiceId), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        await InvoicePaidDomainEventHandler.HandleAsync(
            domainEvent,
            _invoiceRepository,
            _messageBus,
            _logger,
            CancellationToken.None);

        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<InvoicePaidEvent>());
    }

    [Fact]
    public async Task HandleAsync_WhenInvoiceNotFound_LogsWarning()
    {
        Guid invoiceId = Guid.NewGuid();
        InvoicePaidDomainEvent domainEvent = new InvoicePaidDomainEvent(
            invoiceId,
            Guid.NewGuid(),
            DateTime.UtcNow);

        _invoiceRepository.GetByIdAsync(InvoiceId.Create(invoiceId), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        await InvoicePaidDomainEventHandler.HandleAsync(
            domainEvent,
            _invoiceRepository,
            _messageBus,
            _logger,
            CancellationToken.None);

        List<ICall> calls = _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Where(c => c.GetArguments()[0] is LogLevel level && level == LogLevel.Warning)
            .Where(c => LogMessageContains(c, invoiceId.ToString()))
            .ToList();
        calls.Should().HaveCountGreaterThanOrEqualTo(1, "expected at least one Warning log call containing the invoice ID");
    }

    [Fact]
    public async Task HandleAsync_RetrievesInvoiceFromRepository()
    {
        Guid invoiceId = Guid.NewGuid();
        InvoicePaidDomainEvent domainEvent = new InvoicePaidDomainEvent(
            invoiceId,
            Guid.NewGuid(),
            DateTime.UtcNow);

        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, DateTime.UtcNow.AddDays(30));

        _invoiceRepository.GetByIdAsync(InvoiceId.Create(invoiceId), Arg.Any<CancellationToken>())
            .Returns(invoice);

        await InvoicePaidDomainEventHandler.HandleAsync(
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
        InvoicePaidDomainEvent domainEvent = new InvoicePaidDomainEvent(
            invoiceId,
            Guid.NewGuid(),
            DateTime.UtcNow);

        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, DateTime.UtcNow.AddDays(30));

        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        using CancellationTokenSource cts = new CancellationTokenSource();

        await InvoicePaidDomainEventHandler.HandleAsync(
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
        InvoicePaidDomainEvent domainEvent = new InvoicePaidDomainEvent(
            invoiceId,
            Guid.NewGuid(),
            DateTime.UtcNow);

        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, DateTime.UtcNow.AddDays(30));

        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        await InvoicePaidDomainEventHandler.HandleAsync(
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
}
