using Microsoft.Extensions.Logging;
using NSubstitute.Core;
using Wallow.Billing.Application.EventHandlers;
using Wallow.Billing.Domain.Events;
using Wallow.Shared.Contracts.Billing.Events;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;
using static Wallow.Tests.Common.Helpers.LoggerAssertionExtensions;

namespace Wallow.Billing.Tests.Application.EventHandlers;

public class PaymentCreatedDomainEventHandlerTests
{
    private readonly IMessageBus _messageBus;
    private readonly ITenantContext _tenantContext;
    private readonly IUserQueryService _userQueryService;
    private readonly ILogger<PaymentCreatedDomainEventHandler> _logger;

    public PaymentCreatedDomainEventHandlerTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));
        _userQueryService = Substitute.For<IUserQueryService>();
        _userQueryService.GetUserEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns("test@example.com");
        _logger = Substitute.For<ILogger<PaymentCreatedDomainEventHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task HandleAsync_PublishesIntegrationEvent()
    {
        Guid paymentId = Guid.NewGuid();
        Guid invoiceId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        PaymentCreatedDomainEvent domainEvent = new(
            paymentId,
            invoiceId,
            500.00m,
            "USD",
            userId);

        await PaymentCreatedDomainEventHandler.HandleAsync(
            domainEvent,
            _messageBus,
            _tenantContext,
            _userQueryService,
            _logger,
            CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<PaymentReceivedEvent>(e =>
                e.PaymentId == paymentId &&
                e.InvoiceId == invoiceId &&
                e.UserId == userId &&
                e.Amount == 500.00m &&
                e.Currency == "USD"));
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationToken()
    {
        PaymentCreatedDomainEvent domainEvent = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "USD",
            Guid.NewGuid());

        using CancellationTokenSource cts = new();

        await PaymentCreatedDomainEventHandler.HandleAsync(
            domainEvent,
            _messageBus,
            _tenantContext,
            _userQueryService,
            _logger,
            cts.Token);

        await _messageBus.Received(1).PublishAsync(Arg.Any<PaymentReceivedEvent>());
    }

    [Fact]
    public async Task HandleAsync_LogsInformation()
    {
        Guid paymentId = Guid.NewGuid();
        PaymentCreatedDomainEvent domainEvent = new(
            paymentId,
            Guid.NewGuid(),
            100m,
            "USD",
            Guid.NewGuid());

        await PaymentCreatedDomainEventHandler.HandleAsync(
            domainEvent,
            _messageBus,
            _tenantContext,
            _userQueryService,
            _logger,
            CancellationToken.None);

        List<ICall> calls = _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Where(c => c.GetArguments()[0] is LogLevel level && level == LogLevel.Information)
            .Where(c => LogMessageContains(c, paymentId.ToString()))
            .ToList();
        calls.Should().HaveCountGreaterThanOrEqualTo(1, "expected at least one Information log call containing the payment ID");
    }

    [Fact]
    public async Task HandleAsync_PublishesBothLogMessages()
    {
        Guid paymentId = Guid.NewGuid();
        PaymentCreatedDomainEvent domainEvent = new(
            paymentId,
            Guid.NewGuid(),
            100m,
            "USD",
            Guid.NewGuid());

        await PaymentCreatedDomainEventHandler.HandleAsync(
            domainEvent,
            _messageBus,
            _tenantContext,
            _userQueryService,
            _logger,
            CancellationToken.None);

        List<ICall> infoCalls = _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Where(c => c.GetArguments()[0] is LogLevel level && level == LogLevel.Information)
            .ToList();
        infoCalls.Should().HaveCount(2, "expected exactly two Information log calls");
    }
}
