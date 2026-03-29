using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Queries.GetAllInvoices;
using Wallow.Billing.Domain.Entities;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Queries;

public class GetAllInvoicesHandlerTests
{
    private readonly IInvoiceRepository _repository;
    private readonly GetAllInvoicesHandler _handler;

    public GetAllInvoicesHandlerTests()
    {
        _repository = Substitute.For<IInvoiceRepository>();
        _handler = new GetAllInvoicesHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenInvoicesExist_ReturnsAllInvoices()
    {
        Guid userId = Guid.NewGuid();
        Invoice invoice1 = Invoice.Create(userId, "INV-001", "USD", userId, TimeProvider.System);
        Invoice invoice2 = Invoice.Create(userId, "INV-002", "EUR", userId, TimeProvider.System);

        _repository.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Invoice> { invoice1, invoice2 });
        _repository.CountAllAsync(Arg.Any<CancellationToken>())
            .Returns(2);

        Result<PagedResult<InvoiceDto>> result = await _handler.Handle(
            new GetAllInvoicesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items[0].InvoiceNumber.Should().Be("INV-001");
        result.Value.Items[1].InvoiceNumber.Should().Be("INV-002");
    }

    [Fact]
    public async Task Handle_WhenNoInvoices_ReturnsEmptyList()
    {
        _repository.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Invoice>());
        _repository.CountAllAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        Result<PagedResult<InvoiceDto>> result = await _handler.Handle(
            new GetAllInvoicesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }
}
