using Wallow.Billing.Application.Commands.IssueInvoice;
using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Handlers;

public class IssueInvoiceHandlerTests
{
    private readonly IInvoiceRepository _repository;
    private readonly IssueInvoiceHandler _handler;

    public IssueInvoiceHandlerTests()
    {
        _repository = Substitute.For<IInvoiceRepository>();
        _handler = new IssueInvoiceHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithDraftInvoiceWithLineItems_IssuesIt()
    {
        // Arrange
        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, DateTime.UtcNow.AddDays(30));

        // Add a line item so the invoice can be issued
        invoice.AddLineItem("Test Item", Money.Create(100m, "USD"), 1, Guid.NewGuid(), TimeProvider.System);

        _repository.GetByIdWithLineItemsAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        IssueInvoiceCommand command = new(
            InvoiceId: Guid.NewGuid(),
            IssuedByUserId: Guid.NewGuid());

        // Act
        Result<InvoiceDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Issued");

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentInvoice_ReturnsNotFound()
    {
        // Arrange
        _repository.GetByIdWithLineItemsAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        IssueInvoiceCommand command = new(
            InvoiceId: Guid.NewGuid(),
            IssuedByUserId: Guid.NewGuid());

        // Act
        Result<InvoiceDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }
}
