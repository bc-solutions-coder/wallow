using Wallow.Billing.Application.Commands.ProcessPayment;
using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Handlers;

public class ProcessPaymentHandlerTests
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ProcessPaymentHandler _handler;

    public ProcessPaymentHandlerTests()
    {
        _paymentRepository = Substitute.For<IPaymentRepository>();
        _invoiceRepository = Substitute.For<IInvoiceRepository>();
        _handler = new ProcessPaymentHandler(_paymentRepository, _invoiceRepository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidPayment_ProcessesIt()
    {
        // Arrange
        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, DateTime.UtcNow.AddDays(30));
        invoice.AddLineItem("Test Item", Money.Create(100m, "USD"), 1, Guid.NewGuid(), TimeProvider.System);
        invoice.Issue(Guid.NewGuid(), TimeProvider.System);

        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        ProcessPaymentCommand command = new(
            InvoiceId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Amount: 100.00m,
            Currency: "USD",
            PaymentMethod: "CreditCard");

        // Act
        Result<PaymentDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Amount.Should().Be(100.00m);

        _paymentRepository.Received(1).Add(Arg.Any<Payment>());
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentInvoice_ReturnsNotFound()
    {
        // Arrange
        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        ProcessPaymentCommand command = new(
            InvoiceId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Amount: 100.00m,
            Currency: "USD",
            PaymentMethod: "BankTransfer");

        // Act
        Result<PaymentDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WithInvalidPaymentMethod_ReturnsValidationError()
    {
        // Arrange
        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, DateTime.UtcNow.AddDays(30));
        invoice.AddLineItem("Test Item", Money.Create(100m, "USD"), 1, Guid.NewGuid(), TimeProvider.System);
        invoice.Issue(Guid.NewGuid(), TimeProvider.System);

        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        ProcessPaymentCommand command = new(
            InvoiceId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Amount: 100.00m,
            Currency: "USD",
            PaymentMethod: "InvalidMethod");

        // Act
        Result<PaymentDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Validation");
    }

    [Fact]
    public async Task Handle_WhenExecutedTwiceWithSameCommand_CreatesMultiplePayments()
    {
        // Arrange
        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, DateTime.UtcNow.AddDays(30));
        invoice.AddLineItem("Test Item", Money.Create(100m, "USD"), 1, Guid.NewGuid(), TimeProvider.System);
        invoice.Issue(Guid.NewGuid(), TimeProvider.System);

        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        ProcessPaymentCommand command = new(
            InvoiceId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Amount: 100.00m,
            Currency: "USD",
            PaymentMethod: "CreditCard");

        // Act
        Result<PaymentDto> result1 = await _handler.Handle(command, CancellationToken.None);
        Result<PaymentDto> result2 = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.Id.Should().NotBe(result2.Value.Id);
        _paymentRepository.Received(2).Add(Arg.Any<Payment>());
        await _paymentRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExecutedConcurrentlyForSameInvoice_ProcessesAllPayments()
    {
        // Arrange
        Invoice invoice = Invoice.Create(Guid.NewGuid(), "INV-001", "USD", Guid.NewGuid(), TimeProvider.System, DateTime.UtcNow.AddDays(30));
        invoice.AddLineItem("Test Item", Money.Create(300m, "USD"), 1, Guid.NewGuid(), TimeProvider.System);
        invoice.Issue(Guid.NewGuid(), TimeProvider.System);

        _invoiceRepository.GetByIdAsync(Arg.Any<InvoiceId>(), Arg.Any<CancellationToken>())
            .Returns(invoice);

        Guid invoiceId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        ProcessPaymentCommand command1 = new(invoiceId, userId, 100.00m, "USD", "CreditCard");
        ProcessPaymentCommand command2 = new(invoiceId, userId, 100.00m, "USD", "BankTransfer");
        ProcessPaymentCommand command3 = new(invoiceId, userId, 100.00m, "USD", "CreditCard");

        // Act
        Task<Result<PaymentDto>>[] tasks = new[]
        {
            _handler.Handle(command1, CancellationToken.None),
            _handler.Handle(command2, CancellationToken.None),
            _handler.Handle(command3, CancellationToken.None)
        };
        Result<PaymentDto>[] results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        results.Select(r => r.Value.Id).Distinct().Should().HaveCount(3);
        _paymentRepository.Received(3).Add(Arg.Any<Payment>());
        await _paymentRepository.Received(3).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
