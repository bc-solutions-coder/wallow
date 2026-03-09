using Foundry.Billing.Application.Commands.CancelSubscription;
using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Tests.Application.Handlers;

public class CancelSubscriptionHandlerTests
{
    private readonly ISubscriptionRepository _repository;
    private readonly CancelSubscriptionHandler _handler;

    public CancelSubscriptionHandlerTests()
    {
        _repository = Substitute.For<ISubscriptionRepository>();
        _handler = new CancelSubscriptionHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithExistingSubscription_CancelsIt()
    {
        // Arrange
        Guid subscriptionId = Guid.NewGuid();
        Subscription subscription = Subscription.Create(Guid.NewGuid(), "Pro Plan", Money.Create(29.99m, "USD"), DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow.AddDays(30), Guid.NewGuid(), TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<SubscriptionId>(), Arg.Any<CancellationToken>())
            .Returns(subscription);

        CancelSubscriptionCommand command = new(
            SubscriptionId: subscriptionId,
            CancelledByUserId: Guid.NewGuid());

        // Act
        Result<SubscriptionDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentSubscription_ReturnsNotFound()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<SubscriptionId>(), Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        CancelSubscriptionCommand command = new(
            SubscriptionId: Guid.NewGuid(),
            CancelledByUserId: Guid.NewGuid());

        // Act
        Result<SubscriptionDto> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }
}
