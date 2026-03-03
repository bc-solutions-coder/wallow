using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Application.Queries.GetPaymentsByInvoiceId;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Enums;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Tests.Application.Queries;

public class GetPaymentsByInvoiceIdHandlerTests
{
    private readonly IPaymentRepository _repository;
    private readonly GetPaymentsByInvoiceIdHandler _handler;

    public GetPaymentsByInvoiceIdHandlerTests()
    {
        _repository = Substitute.For<IPaymentRepository>();
        _handler = new GetPaymentsByInvoiceIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenPaymentsExist_ReturnsPaymentDtos()
    {
        Guid userId = Guid.NewGuid();
        InvoiceId invoiceId = InvoiceId.New();
        Payment payment1 = Payment.Create(invoiceId, userId, Money.Create(100m, "USD"), PaymentMethod.CreditCard, userId, TimeProvider.System);
        Payment payment2 = Payment.Create(invoiceId, userId, Money.Create(200m, "USD"), PaymentMethod.BankTransfer, userId, TimeProvider.System);

        _repository.GetByInvoiceIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(new List<Payment> { payment1, payment2 });

        GetPaymentsByInvoiceIdQuery query = new(invoiceId.Value);

        Result<IReadOnlyList<PaymentDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Amount.Should().Be(100m);
        result.Value[1].Amount.Should().Be(200m);
    }

    [Fact]
    public async Task Handle_WhenNoPaymentsExist_ReturnsEmptyList()
    {
        _repository.GetByInvoiceIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(new List<Payment>());

        GetPaymentsByInvoiceIdQuery query = new(Guid.NewGuid());

        Result<IReadOnlyList<PaymentDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_QueriesCorrectInvoiceId()
    {
        Guid invoiceId = Guid.NewGuid();

        _repository.GetByInvoiceIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(new List<Payment>());

        GetPaymentsByInvoiceIdQuery query = new(invoiceId);

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetByInvoiceIdAsync(
            Arg.Is<InvoiceId>(id => id.Value == invoiceId),
            Arg.Any<CancellationToken>());
    }
}
