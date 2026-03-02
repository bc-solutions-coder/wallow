using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Application.Queries.GetInvoiceById;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Application.Tests.Queries;

public class GetInvoiceByIdHandlerTests
{
    private readonly IInvoiceRepository _repository;
    private readonly GetInvoiceByIdHandler _handler;

    public GetInvoiceByIdHandlerTests()
    {
        _repository = Substitute.For<IInvoiceRepository>();
        _handler = new GetInvoiceByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenInvoiceExists_ReturnsInvoiceDto()
    {
        Guid userId = Guid.NewGuid();
        Invoice invoice = Invoice.Create(userId, "INV-001", "USD", userId, TimeProvider.System);

        _repository.GetByIdWithLineItemsAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        GetInvoiceByIdQuery query = new(invoice.Id.Value);

        Result<InvoiceDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.InvoiceNumber.Should().Be("INV-001");
        result.Value.Currency.Should().Be("USD");
        result.Value.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_WhenInvoiceNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdWithLineItemsAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        GetInvoiceByIdQuery query = new(Guid.NewGuid());

        Result<InvoiceDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_RetrievesInvoiceWithLineItems()
    {
        Guid invoiceId = Guid.NewGuid();
        GetInvoiceByIdQuery query = new(invoiceId);

        _repository.GetByIdWithLineItemsAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetByIdWithLineItemsAsync(
            Arg.Is<InvoiceId>(id => id.Value == invoiceId),
            Arg.Any<CancellationToken>());
    }
}
