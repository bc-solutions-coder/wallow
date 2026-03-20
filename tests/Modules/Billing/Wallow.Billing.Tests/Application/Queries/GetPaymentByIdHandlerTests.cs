using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Queries.GetPaymentById;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Enums;
using Wallow.Billing.Domain.Identity;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Queries;

public class GetPaymentByIdHandlerTests
{
    private readonly IPaymentRepository _repository;
    private readonly GetPaymentByIdHandler _handler;

    public GetPaymentByIdHandlerTests()
    {
        _repository = Substitute.For<IPaymentRepository>();
        _handler = new GetPaymentByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenPaymentExists_ReturnsPaymentDto()
    {
        Guid userId = Guid.NewGuid();
        InvoiceId invoiceId = InvoiceId.New();
        Payment payment = Payment.Create(invoiceId, userId, Money.Create(250m, "USD"), PaymentMethod.CreditCard, userId, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<PaymentId>(), Arg.Any<CancellationToken>())
            .Returns(payment);

        GetPaymentByIdQuery query = new(payment.Id.Value);

        Result<PaymentDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(250m);
        result.Value.Currency.Should().Be("USD");
        result.Value.Method.Should().Be("CreditCard");
        result.Value.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_WhenPaymentNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<PaymentId>(), Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        GetPaymentByIdQuery query = new(Guid.NewGuid());

        Result<PaymentDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_QueriesCorrectPaymentId()
    {
        Guid paymentId = Guid.NewGuid();

        _repository.GetByIdAsync(Arg.Any<PaymentId>(), Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        GetPaymentByIdQuery query = new(paymentId);

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetByIdAsync(
            Arg.Is<PaymentId>(id => id.Value == paymentId),
            Arg.Any<CancellationToken>());
    }
}
