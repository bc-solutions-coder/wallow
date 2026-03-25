using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Queries.GetAllSubscriptions;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Tests.Application.Queries;

public class GetAllSubscriptionsHandlerTests
{
    private readonly ISubscriptionRepository _repository;
    private readonly GetAllSubscriptionsHandler _handler;

    public GetAllSubscriptionsHandlerTests()
    {
        _repository = Substitute.For<ISubscriptionRepository>();
        _handler = new GetAllSubscriptionsHandler(_repository);
    }

    [Fact]
    public async Task Handle_WhenSubscriptionsExist_ReturnsAllSubscriptions()
    {
        Guid userId = Guid.NewGuid();
        Money price = Money.Create(9.99m, "USD");
        DateTime start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        Subscription sub1 = Subscription.Create(userId, "Basic", price, start, periodEnd, userId, TimeProvider.System);
        Subscription sub2 = Subscription.Create(userId, "Pro", price, start, periodEnd, userId, TimeProvider.System);

        _repository.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Subscription> { sub1, sub2 });
        _repository.CountAllAsync(Arg.Any<CancellationToken>())
            .Returns(2);

        Result<PagedResult<SubscriptionDto>> result = await _handler.Handle(
            new GetAllSubscriptionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items[0].PlanName.Should().Be("Basic");
        result.Value.Items[1].PlanName.Should().Be("Pro");
    }

    [Fact]
    public async Task Handle_WhenNoSubscriptions_ReturnsEmptyList()
    {
        _repository.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Subscription>());
        _repository.CountAllAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        Result<PagedResult<SubscriptionDto>> result = await _handler.Handle(
            new GetAllSubscriptionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PaginationMath_PageIsSkipDividedByTakePlusOne()
    {
        _repository.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Subscription>());
        _repository.CountAllAsync(Arg.Any<CancellationToken>())
            .Returns(0);

        Result<PagedResult<SubscriptionDto>> result = await _handler.Handle(
            new GetAllSubscriptionsQuery(Skip: 100, Take: 50), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(3);
    }
}
