using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Queries.GetInvoicesByUserId;
using Wallow.Billing.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Queries;

public class GetInvoicesByUserIdHandlerTests
{
    private readonly IInvoiceRepository _repository;
    private readonly GetInvoicesByUserIdHandler _handler;

    public GetInvoicesByUserIdHandlerTests()
    {
        _repository = Substitute.For<IInvoiceRepository>();
        _handler = new GetInvoicesByUserIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenInvoicesExist_ReturnsUserInvoices()
    {
        Guid userId = Guid.NewGuid();
        Invoice invoice1 = Invoice.Create(userId, "INV-001", "USD", userId, TimeProvider.System);
        Invoice invoice2 = Invoice.Create(userId, "INV-002", "EUR", userId, TimeProvider.System);

        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Invoice> { invoice1, invoice2 });

        GetInvoicesByUserIdQuery query = new(userId);

        Result<IReadOnlyList<InvoiceDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(dto => dto.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task Handle_WhenNoInvoicesForUser_ReturnsEmptyList()
    {
        Guid userId = Guid.NewGuid();

        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Invoice>());

        GetInvoicesByUserIdQuery query = new(userId);

        Result<IReadOnlyList<InvoiceDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_QueriesCorrectUserId()
    {
        Guid userId = Guid.NewGuid();

        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Invoice>());

        GetInvoicesByUserIdQuery query = new(userId);

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }
}
