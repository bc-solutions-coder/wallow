using Foundry.Billing.Application.Commands.AddLineItem;
using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Tests.Application.Handlers;

public class AddLineItemHandlerTests
{
    private readonly IInvoiceRepository _repository;
    private readonly AddLineItemHandler _handler;

    public AddLineItemHandlerTests()
    {
        _repository = Substitute.For<IInvoiceRepository>();
        _handler = new AddLineItemHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsLineItemAndReturnsSuccess()
    {
        Guid userId = Guid.NewGuid();
        Invoice invoice = Invoice.Create(userId, "INV-001", "USD", userId, TimeProvider.System);

        _repository.GetByIdWithLineItemsAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        AddLineItemCommand command = new(
            InvoiceId: invoice.Id.Value,
            Description: "Consulting Services",
            UnitPrice: 150.00m,
            Quantity: 2,
            UpdatedByUserId: userId);

        Result<InvoiceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LineItems.Should().ContainSingle();
        result.Value.LineItems[0].Description.Should().Be("Consulting Services");
        result.Value.LineItems[0].UnitPrice.Should().Be(150.00m);
        result.Value.LineItems[0].Quantity.Should().Be(2);
        result.Value.LineItems[0].LineTotal.Should().Be(300.00m);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenInvoiceNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdWithLineItemsAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        AddLineItemCommand command = new(
            InvoiceId: Guid.NewGuid(),
            Description: "Service",
            UnitPrice: 100m,
            Quantity: 1,
            UpdatedByUserId: Guid.NewGuid());

        Result<InvoiceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMultipleLineItems_AccumulatesCorrectly()
    {
        Guid userId = Guid.NewGuid();
        Invoice invoice = Invoice.Create(userId, "INV-002", "USD", userId, TimeProvider.System);
        invoice.AddLineItem("First Item", Money.Create(100m, "USD"), 1, userId, TimeProvider.System);

        _repository.GetByIdWithLineItemsAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        AddLineItemCommand command = new(
            InvoiceId: invoice.Id.Value,
            Description: "Second Item",
            UnitPrice: 50m,
            Quantity: 3,
            UpdatedByUserId: userId);

        Result<InvoiceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LineItems.Should().HaveCount(2);
        result.Value.TotalAmount.Should().Be(250m);
    }
}
