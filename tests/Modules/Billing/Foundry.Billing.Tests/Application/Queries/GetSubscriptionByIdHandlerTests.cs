using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Application.Queries.GetSubscriptionById;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Application.Tests.Queries;

public class GetSubscriptionByIdHandlerTests
{
    private readonly ISubscriptionRepository _repository;
    private readonly GetSubscriptionByIdHandler _handler;

    public GetSubscriptionByIdHandlerTests()
    {
        _repository = Substitute.For<ISubscriptionRepository>();
        _handler = new GetSubscriptionByIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenSubscriptionExists_ReturnsSubscriptionDto()
    {
        Guid userId = Guid.NewGuid();
        Subscription subscription = Subscription.Create(userId, "Pro Plan", Money.Create(29.99m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), userId, TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<SubscriptionId>(), Arg.Any<CancellationToken>())
            .Returns(subscription);

        GetSubscriptionByIdQuery query = new(subscription.Id.Value);

        Result<SubscriptionDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PlanName.Should().Be("Pro Plan");
        result.Value.Price.Should().Be(29.99m);
        result.Value.Currency.Should().Be("USD");
        result.Value.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_WhenSubscriptionNotFound_ReturnsNotFoundError()
    {
        _repository.GetByIdAsync(Arg.Any<SubscriptionId>(), Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        GetSubscriptionByIdQuery query = new(Guid.NewGuid());

        Result<SubscriptionDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_QueriesCorrectSubscriptionId()
    {
        Guid subscriptionId = Guid.NewGuid();

        _repository.GetByIdAsync(Arg.Any<SubscriptionId>(), Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        GetSubscriptionByIdQuery query = new(subscriptionId);

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetByIdAsync(
            Arg.Is<SubscriptionId>(id => id.Value == subscriptionId),
            Arg.Any<CancellationToken>());
    }
}
