using Foundry.Billing.Application.Commands.CancelInvoice;
using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Application.Tests.Handlers;

public class CancelInvoiceHandlerTests
{
    private readonly IInvoiceRepository _repository;
    private readonly CancelInvoiceHandler _handler;

    public CancelInvoiceHandlerTests()
    {
        _repository = Substitute.For<IInvoiceRepository>();
        _handler = new CancelInvoiceHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithExistingInvoice_CancelsAndReturnsSuccess()
    {
        Guid userId = Guid.NewGuid();
        Invoice invoice = Invoice.Create(userId, "INV-001", "USD", userId, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        CancelInvoiceCommand command = new(
            InvoiceId: invoice.Id.Value,
            CancelledByUserId: userId);

        Result<InvoiceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenInvoiceNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        CancelInvoiceCommand command = new(
            InvoiceId: Guid.NewGuid(),
            CancelledByUserId: Guid.NewGuid());

        Result<InvoiceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithIssuedInvoice_CancelsSuccessfully()
    {
        Guid userId = Guid.NewGuid();
        Invoice invoice = Invoice.Create(userId, "INV-002", "USD", userId, TimeProvider.System);
        invoice.AddLineItem("Service", Money.Create(100m, "USD"), 1, userId, TimeProvider.System);
        invoice.Issue(userId, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        CancelInvoiceCommand command = new(
            InvoiceId: invoice.Id.Value,
            CancelledByUserId: userId);

        Result<InvoiceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");
    }
}
