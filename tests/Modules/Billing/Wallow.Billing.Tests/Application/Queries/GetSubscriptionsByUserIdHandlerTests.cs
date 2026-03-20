using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Queries.GetSubscriptionsByUserId;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Queries;

public class GetSubscriptionsByUserIdHandlerTests
{
    private readonly ISubscriptionRepository _repository;
    private readonly GetSubscriptionsByUserIdHandler _handler;

    public GetSubscriptionsByUserIdHandlerTests()
    {
        _repository = Substitute.For<ISubscriptionRepository>();
        _handler = new GetSubscriptionsByUserIdHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenSubscriptionsExist_ReturnsUserSubscriptions()
    {
        Guid userId = Guid.NewGuid();
        Subscription sub1 = Subscription.Create(userId, "Pro Plan", Money.Create(29.99m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), userId, TimeProvider.System);
        Subscription sub2 = Subscription.Create(userId, "Enterprise Plan", Money.Create(99.99m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), userId, TimeProvider.System);

        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Subscription> { sub1, sub2 });

        GetSubscriptionsByUserIdQuery query = new(userId);

        Result<IReadOnlyList<SubscriptionDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(dto => dto.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task Handle_WhenNoSubscriptionsForUser_ReturnsEmptyList()
    {
        Guid userId = Guid.NewGuid();

        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Subscription>());

        GetSubscriptionsByUserIdQuery query = new(userId);

        Result<IReadOnlyList<SubscriptionDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_QueriesCorrectUserId()
    {
        Guid userId = Guid.NewGuid();

        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Subscription>());

        GetSubscriptionsByUserIdQuery query = new(userId);

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).GetByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }
}
