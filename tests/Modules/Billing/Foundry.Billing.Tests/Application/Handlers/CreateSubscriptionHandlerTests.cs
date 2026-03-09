using Foundry.Billing.Application.Commands.CreateSubscription;
using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Tests.Application.Handlers;

public class CreateSubscriptionHandlerTests
{
    private readonly ISubscriptionRepository _repository;
    private readonly CreateSubscriptionHandler _handler;

    public CreateSubscriptionHandlerTests()
    {
        _repository = Substitute.For<ISubscriptionRepository>();
        _handler = new CreateSubscriptionHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesSubscription()
    {
        // Arrange
        CreateSubscriptionCommand command = new(
            UserId: Guid.NewGuid(),
            PlanName: "Pro Plan",
            Price: 29.99m,
            Currency: "USD",
            StartDate: DateTime.UtcNow,
            PeriodEnd: DateTime.UtcNow.AddDays(30));

        // Act
        Result<SubscriptionDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.PlanName.Should().Be("Pro Plan");
        result.Value.Status.Should().Be("Active");

        _repository.Received(1).Add(Arg.Any<Subscription>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDifferentCurrency_CreatesSubscriptionWithCurrency()
    {
        // Arrange
        CreateSubscriptionCommand command = new(
            UserId: Guid.NewGuid(),
            PlanName: "Enterprise",
            Price: 199.00m,
            Currency: "EUR",
            StartDate: DateTime.UtcNow,
            PeriodEnd: DateTime.UtcNow.AddDays(365));

        // Act
        Result<SubscriptionDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PlanName.Should().Be("Enterprise");
    }
}
