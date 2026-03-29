using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Queries.GetAllPayments;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Enums;
using Wallow.Billing.Domain.Identity;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Queries;

public class GetAllPaymentsHandlerTests
{
    private readonly IPaymentRepository _repository;
    private readonly GetAllPaymentsHandler _handler;

    public GetAllPaymentsHandlerTests()
    {
        _repository = Substitute.For<IPaymentRepository>();
        _handler = new GetAllPaymentsHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenPaymentsExist_ReturnsAllPayments()
    {
        Guid userId = Guid.NewGuid();
        Payment payment1 = Payment.Create(
            InvoiceId.New(), userId, Money.Create(100m, "USD"),
            PaymentMethod.CreditCard, userId, TimeProvider.System);
        Payment payment2 = Payment.Create(
            InvoiceId.New(), userId, Money.Create(200m, "EUR"),
            PaymentMethod.BankTransfer, userId, TimeProvider.System);

        _repository.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Payment> { payment1, payment2 });
        _repository.CountAllAsync(Arg.Any<CancellationToken>())
            .Returns(2);

        Result<PagedResult<PaymentDto>> result = await _handler.Handle(
            new GetAllPaymentsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items[0].Amount.Should().Be(100m);
        result.Value.Items[1].Amount.Should().Be(200m);
    }

    [Fact]
    public async Task Handle_WhenNoPayments_ReturnsEmptyList()
    {
        _repository.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Payment>());
        _repository.CountAllAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        Result<PagedResult<PaymentDto>> result = await _handler.Handle(
            new GetAllPaymentsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PaginationMath_PageIsSkipDividedByTakePlusOne()
    {
        _repository.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Payment>());
        _repository.CountAllAsync(Arg.Any<CancellationToken>())
            .Returns(100);

        Result<PagedResult<PaymentDto>> result = await _handler.Handle(
            new GetAllPaymentsQuery(Skip: 50, Take: 25), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(3);
    }
}
