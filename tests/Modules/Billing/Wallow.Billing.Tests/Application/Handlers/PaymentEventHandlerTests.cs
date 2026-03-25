using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Billing.Application.EventHandlers;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Events;
using Wallow.Billing.Domain.Identity;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Contracts.Billing.Events;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Billing.Tests.Application.Handlers;

public class PaymentEventHandlerTests
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMessageBus _messageBus;
    private readonly ITenantContext _tenantContext;
    private readonly IUserQueryService _userQueryService;
    private readonly ILogger<InvoicePaidDomainEventHandler> _invoicePaidLogger;
    private readonly ILogger<PaymentCreatedDomainEventHandler> _paymentReceivedLogger;

    public PaymentEventHandlerTests()
    {
        _invoiceRepository = Substitute.For<IInvoiceRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        _tenantContext = Substitute.For<ITenantContext>();
        _userQueryService = Substitute.For<IUserQueryService>();
        _userQueryService.GetUserEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns("test@example.com");
        _invoicePaidLogger = NullLogger<InvoicePaidDomainEventHandler>.Instance;
        _paymentReceivedLogger = NullLogger<PaymentCreatedDomainEventHandler>.Instance;

        TenantId tenantId = TenantId.New();
        _tenantContext.TenantId.Returns(tenantId);
    }

    // --- InvoicePaidDomainEventHandler Tests ---

    [Fact]
    public async Task InvoicePaid_WithValidInvoice_PublishesIntegrationEvent()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Invoice invoice = Invoice.Create(userId, "INV-001", "USD", userId, TimeProvider.System, DateTime.UtcNow.AddDays(30));
        invoice.AddLineItem("Service", Money.Create(100m, "USD"), 1, userId, TimeProvider.System);
        invoice.Issue(userId, TimeProvider.System);
        invoice.MarkAsPaid(Guid.NewGuid(), userId, TimeProvider.System);

        InvoicePaidDomainEvent domainEvent = new(
            invoice.Id.Value,
            Guid.NewGuid(),
            DateTime.UtcNow);

        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        // Act
        await InvoicePaidDomainEventHandler.HandleAsync(
            domainEvent, _invoiceRepository, _userQueryService, _messageBus, _invoicePaidLogger, CancellationToken.None);

        // Assert
        await _messageBus.Received(1).PublishAsync(Arg.Is<InvoicePaidEvent>(e =>
            e.InvoiceId == domainEvent.InvoiceId &&
            e.PaymentId == domainEvent.PaymentId &&
            e.UserId == userId &&
            e.InvoiceNumber == "INV-001" &&
            e.Currency == "USD"));
    }

    [Fact]
    public async Task InvoicePaid_WhenInvoiceNotFound_DoesNotPublishEvent()
    {
        // Arrange
        InvoicePaidDomainEvent domainEvent = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        // Act
        await InvoicePaidDomainEventHandler.HandleAsync(
            domainEvent, _invoiceRepository, _userQueryService, _messageBus, _invoicePaidLogger, CancellationToken.None);

        // Assert
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<InvoicePaidEvent>());
    }

    [Fact]
    public async Task InvoicePaid_CalledTwiceWithSameEvent_PublishesTwice()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Invoice invoice = Invoice.Create(userId, "INV-DUP", "USD", userId, TimeProvider.System, DateTime.UtcNow.AddDays(30));
        invoice.AddLineItem("Service", Money.Create(50m, "USD"), 2, userId, TimeProvider.System);
        invoice.Issue(userId, TimeProvider.System);
        invoice.MarkAsPaid(Guid.NewGuid(), userId, TimeProvider.System);

        InvoicePaidDomainEvent domainEvent = new(
            invoice.Id.Value,
            Guid.NewGuid(),
            DateTime.UtcNow);

        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        // Act
        await InvoicePaidDomainEventHandler.HandleAsync(
            domainEvent, _invoiceRepository, _userQueryService, _messageBus, _invoicePaidLogger, CancellationToken.None);
        await InvoicePaidDomainEventHandler.HandleAsync(
            domainEvent, _invoiceRepository, _userQueryService, _messageBus, _invoicePaidLogger, CancellationToken.None);

        // Assert - handler is stateless, so duplicate calls just publish again (idempotency is upstream)
        await _messageBus.Received(2).PublishAsync(Arg.Any<InvoicePaidEvent>());
    }

    // --- PaymentCreatedDomainEventHandler Tests ---

    [Fact]
    public async Task PaymentReceived_WithValidEvent_PublishesIntegrationEvent()
    {
        // Arrange
        Guid paymentId = Guid.NewGuid();
        Guid invoiceId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        PaymentCreatedDomainEvent domainEvent = new(
            paymentId, invoiceId, 250.00m, "EUR", userId);

        // Act
        await PaymentCreatedDomainEventHandler.HandleAsync(
            domainEvent, _messageBus, _tenantContext, _userQueryService, _paymentReceivedLogger, CancellationToken.None);

        // Assert
        await _messageBus.Received(1).PublishAsync(Arg.Is<PaymentReceivedEvent>(e =>
            e.PaymentId == paymentId &&
            e.InvoiceId == invoiceId &&
            e.UserId == userId &&
            e.Amount == 250.00m &&
            e.Currency == "EUR" &&
            e.TenantId == _tenantContext.TenantId.Value));
    }

    [Fact]
    public async Task PaymentReceived_SetsUserEmailFromQueryService_AndEmptyPaymentMethod()
    {
        // Arrange
        PaymentCreatedDomainEvent domainEvent = new(
            Guid.NewGuid(), Guid.NewGuid(), 100m, "USD", Guid.NewGuid());

        // Act
        await PaymentCreatedDomainEventHandler.HandleAsync(
            domainEvent, _messageBus, _tenantContext, _userQueryService, _paymentReceivedLogger, CancellationToken.None);

        // Assert
        await _messageBus.Received(1).PublishAsync(Arg.Is<PaymentReceivedEvent>(e =>
            e.UserEmail == "test@example.com" &&
            e.PaymentMethod == string.Empty));
    }

    [Fact]
    public async Task PaymentReceived_CalledTwiceWithSameEvent_PublishesTwice()
    {
        // Arrange
        PaymentCreatedDomainEvent domainEvent = new(
            Guid.NewGuid(), Guid.NewGuid(), 75m, "GBP", Guid.NewGuid());

        // Act
        await PaymentCreatedDomainEventHandler.HandleAsync(
            domainEvent, _messageBus, _tenantContext, _userQueryService, _paymentReceivedLogger, CancellationToken.None);
        await PaymentCreatedDomainEventHandler.HandleAsync(
            domainEvent, _messageBus, _tenantContext, _userQueryService, _paymentReceivedLogger, CancellationToken.None);

        // Assert - handler is stateless, duplicate calls publish again
        await _messageBus.Received(2).PublishAsync(Arg.Any<PaymentReceivedEvent>());
    }
}
