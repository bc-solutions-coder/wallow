using Wallow.Billing.Application.Commands.CreateInvoice;
using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Handlers;

public class CreateInvoiceHandlerTests
{
    private readonly IInvoiceRepository _repository;
    private readonly CreateInvoiceHandler _handler;

    public CreateInvoiceHandlerTests()
    {
        _repository = Substitute.For<IInvoiceRepository>();
        _handler = new CreateInvoiceHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesInvoice()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        CreateInvoiceCommand command = new(
            UserId: userId,
            InvoiceNumber: "INV-001",
            Currency: "USD",
            DueDate: DateTime.UtcNow.AddDays(30));

        _repository.ExistsByInvoiceNumberAsync(command.InvoiceNumber, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<InvoiceDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.InvoiceNumber.Should().Be("INV-001");
        result.Value.Currency.Should().Be("USD");

        _repository.Received(1).Add(Arg.Any<Invoice>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateInvoiceNumber_ReturnsFailure()
    {
        // Arrange
        CreateInvoiceCommand command = new(
            UserId: Guid.NewGuid(),
            InvoiceNumber: "INV-001",
            Currency: "USD",
            DueDate: null);

        _repository.ExistsByInvoiceNumberAsync(command.InvoiceNumber, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result<InvoiceDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Conflict");

        _repository.DidNotReceive().Add(Arg.Any<Invoice>());
    }

    [Fact]
    public async Task Handle_WithCustomFields_IncludesThemInInvoice()
    {
        // Arrange
        Dictionary<string, object> customFields = new Dictionary<string, object>
        {
            { "PO_Number", "PO-12345" },
            { "Department", "Engineering" }
        };

        CreateInvoiceCommand command = new(
            UserId: Guid.NewGuid(),
            InvoiceNumber: "INV-002",
            Currency: "EUR",
            DueDate: DateTime.UtcNow.AddDays(60),
            CustomFields: customFields);

        _repository.ExistsByInvoiceNumberAsync(command.InvoiceNumber, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<InvoiceDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task Handle_WhenExecutedTwiceWithSameInvoiceNumber_SecondExecutionFails()
    {
        // Arrange
        CreateInvoiceCommand command = new(
            UserId: Guid.NewGuid(),
            InvoiceNumber: "INV-DUPLICATE",
            Currency: "USD",
            DueDate: DateTime.UtcNow.AddDays(30));

        _repository.ExistsByInvoiceNumberAsync(command.InvoiceNumber, Arg.Any<CancellationToken>())
            .Returns(false, true);

        // Act
        Result<InvoiceDto> result1 = await _handler.Handle(command, CancellationToken.None);
        Result<InvoiceDto> result2 = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsFailure.Should().BeTrue();
        result2.Error.Code.Should().Contain("Conflict");
        _repository.Received(1).Add(Arg.Any<Invoice>());
    }

    [Fact]
    public async Task Handle_WhenExecutedConcurrentlyWithDifferentInvoiceNumbers_CreatesAllInvoices()
    {
        // Arrange
        _repository.ExistsByInvoiceNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        Guid userId = Guid.NewGuid();
        CreateInvoiceCommand command1 = new(userId, "INV-001", "USD", DateTime.UtcNow.AddDays(30));
        CreateInvoiceCommand command2 = new(userId, "INV-002", "USD", DateTime.UtcNow.AddDays(30));
        CreateInvoiceCommand command3 = new(userId, "INV-003", "EUR", DateTime.UtcNow.AddDays(60));

        // Act
        Task<Result<InvoiceDto>>[] tasks = new[]
        {
            _handler.Handle(command1, CancellationToken.None),
            _handler.Handle(command2, CancellationToken.None),
            _handler.Handle(command3, CancellationToken.None)
        };
        Result<InvoiceDto>[] results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        results.Select(r => r.Value.Id).Distinct().Should().HaveCount(3);
        _repository.Received(3).Add(Arg.Any<Invoice>());
        await _repository.Received(3).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
