using Wallow.Billing.Application.EventHandlers;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Events;
using Wallow.Billing.Domain.Identity;
using Wallow.Shared.Contracts.Billing.Events;
using Wallow.Shared.Contracts.Identity;
using Wallow.Tests.Common.Builders;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Wallow.Billing.Tests.Application.Handlers;

public class InvoiceOverdueDomainEventHandlerTests
{
    private readonly IInvoiceRepository _repository;
    private readonly IUserQueryService _userQueryService;
    private readonly IMessageBus _bus;
    private readonly ILogger<InvoiceOverdueDomainEventHandler> _logger;

    public InvoiceOverdueDomainEventHandlerTests()
    {
        _repository = Substitute.For<IInvoiceRepository>();
        _userQueryService = Substitute.For<IUserQueryService>();
        _userQueryService.GetUserEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns("test@example.com");
        _bus = Substitute.For<IMessageBus>();
        _logger = Substitute.For<ILogger<InvoiceOverdueDomainEventHandler>>();
    }

    [Fact]
    public async Task HandleAsync_WithExistingInvoice_PublishesInvoiceOverdueEvent()
    {
        // Arrange
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .AsOverdue()
            .Build();

        Guid userId = Guid.NewGuid();
        DateTime dueDate = DateTime.UtcNow.AddDays(-1);
        InvoiceOverdueDomainEvent domainEvent = new(invoice.Id.Value, userId, dueDate);

        _repository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        // Act
        await InvoiceOverdueDomainEventHandler.HandleAsync(
            domainEvent, _repository, _userQueryService, _bus, _logger, CancellationToken.None);

        // Assert
        await _bus.Received(1).PublishAsync(Arg.Is<InvoiceOverdueEvent>(e =>
            e.InvoiceId == invoice.Id.Value &&
            e.UserId == userId &&
            e.DueDate == dueDate &&
            e.InvoiceNumber == invoice.InvoiceNumber &&
            e.Amount == invoice.TotalAmount.Amount &&
            e.Currency == invoice.TotalAmount.Currency));
    }

    [Fact]
    public async Task HandleAsync_WhenInvoiceNotFound_DoesNotPublishEvent()
    {
        // Arrange
        InvoiceOverdueDomainEvent domainEvent = new(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        _repository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        // Act
        await InvoiceOverdueDomainEventHandler.HandleAsync(
            domainEvent, _repository, _userQueryService, _bus, _logger, CancellationToken.None);

        // Assert
        await _bus.DidNotReceive().PublishAsync(Arg.Any<InvoiceOverdueEvent>());
    }
}

public class InvoiceCreatedDomainEventHandlerTests
{
    private readonly IInvoiceRepository _repository;
    private readonly IMessageBus _bus;
    private readonly ILogger<InvoiceCreatedDomainEventHandler> _logger;

    public InvoiceCreatedDomainEventHandlerTests()
    {
        _repository = Substitute.For<IInvoiceRepository>();
        _bus = Substitute.For<IMessageBus>();
        _logger = Substitute.For<ILogger<InvoiceCreatedDomainEventHandler>>();
    }

    [Fact]
    public async Task HandleAsync_WithExistingInvoice_PublishesInvoiceCreatedEvent()
    {
        // Arrange
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .Build();

        InvoiceCreatedDomainEvent domainEvent = new(
            invoice.Id.Value, Guid.NewGuid(), 100m, "USD");

        _repository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        // Act
        await InvoiceCreatedDomainEventHandler.HandleAsync(
            domainEvent, _repository, _bus, _logger, CancellationToken.None);

        // Assert
        await _bus.Received(1).PublishAsync(Arg.Is<InvoiceCreatedEvent>(e =>
            e.InvoiceId == domainEvent.InvoiceId &&
            e.UserId == domainEvent.UserId &&
            e.Amount == domainEvent.TotalAmount &&
            e.Currency == domainEvent.Currency &&
            e.InvoiceNumber == invoice.InvoiceNumber &&
            e.TenantId == invoice.TenantId.Value));
    }

    [Fact]
    public async Task HandleAsync_WhenInvoiceNotFound_PublishesEventWithFallbackValues()
    {
        // Arrange
        InvoiceCreatedDomainEvent domainEvent = new(Guid.NewGuid(), Guid.NewGuid(), 50m, "EUR");

        _repository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        // Act
        await InvoiceCreatedDomainEventHandler.HandleAsync(
            domainEvent, _repository, _bus, _logger, CancellationToken.None);

        // Assert
        await _bus.Received(1).PublishAsync(Arg.Is<InvoiceCreatedEvent>(e =>
            e.InvoiceId == domainEvent.InvoiceId &&
            e.TenantId == Guid.Empty &&
            e.InvoiceNumber == string.Empty &&
            e.Amount == domainEvent.TotalAmount &&
            e.Currency == domainEvent.Currency));
    }

    [Fact]
    public async Task HandleAsync_WithInvoiceDueDate_UsesInvoiceDueDate()
    {
        // Arrange
        DateTime dueDate = DateTime.UtcNow.AddDays(15);
        Invoice invoice = InvoiceBuilder.Create()
            .WithDefaultLineItem()
            .WithDueDate(dueDate)
            .Build();

        InvoiceCreatedDomainEvent domainEvent = new(
            invoice.Id.Value, Guid.NewGuid(), 100m, "USD");

        _repository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        // Act
        await InvoiceCreatedDomainEventHandler.HandleAsync(
            domainEvent, _repository, _bus, _logger, CancellationToken.None);

        // Assert
        await _bus.Received(1).PublishAsync(Arg.Is<InvoiceCreatedEvent>(e =>
            e.DueDate == dueDate));
    }
}
