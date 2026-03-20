using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Enums;
using Wallow.Billing.Domain.Identity;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Billing.Infrastructure.Persistence.Repositories;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Billing.Tests.Infrastructure.Persistence;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class SubscriptionRepositoryTests(PostgresContainerFixture fixture) : DbContextIntegrationTestBase<BillingDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    private SubscriptionRepository CreateRepository() => new(DbContext);

    [Fact]
    public async Task Add_And_GetByIdAsync_ReturnsSubscription()
    {
        SubscriptionRepository repository = CreateRepository();
        Subscription subscription = Subscription.Create(TestUserId, "pro", Money.Create(29.99m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), TestUserId, TimeProvider.System);

        repository.Add(subscription);
        await repository.SaveChangesAsync();

        Subscription? result = await repository.GetByIdAsync(subscription.Id);

        result.Should().NotBeNull();
        result.PlanName.Should().Be("pro");
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsSubscriptionsForUser()
    {
        SubscriptionRepository repository = CreateRepository();
        Guid userId = Guid.NewGuid();
        Subscription sub1 = Subscription.Create(userId, "free", Money.Create(0m, "USD"), DateTime.UtcNow.AddMonths(-2), DateTime.UtcNow.AddMonths(-1), TestUserId, TimeProvider.System);
        Subscription sub2 = Subscription.Create(userId, "pro", Money.Create(29.99m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), TestUserId, TimeProvider.System);

        repository.Add(sub1);
        repository.Add(sub2);
        await repository.SaveChangesAsync();

        IReadOnlyList<Subscription> result = await repository.GetByUserIdAsync(userId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllSubscriptions()
    {
        SubscriptionRepository repository = CreateRepository();
        Subscription subscription = Subscription.Create(TestUserId, "enterprise", Money.Create(99.99m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), TestUserId, TimeProvider.System);

        repository.Add(subscription);
        await repository.SaveChangesAsync();

        IReadOnlyList<Subscription> result = await repository.GetAllAsync();

        result.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetActiveByUserIdAsync_ReturnsActiveSubscription()
    {
        SubscriptionRepository repository = CreateRepository();
        Guid userId = Guid.NewGuid();
        Subscription activeSubscription = Subscription.Create(userId, "pro", Money.Create(29.99m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), TestUserId, TimeProvider.System);

        repository.Add(activeSubscription);
        await repository.SaveChangesAsync();

        Subscription? result = await repository.GetActiveByUserIdAsync(userId);

        result.Should().NotBeNull();
        result.Status.Should().Be(SubscriptionStatus.Active);
        result.PlanName.Should().Be("pro");
    }

    [Fact]
    public async Task GetActiveByUserIdAsync_WhenNoActive_ReturnsNull()
    {
        SubscriptionRepository repository = CreateRepository();
        Guid userId = Guid.NewGuid();
        Subscription cancelled = Subscription.Create(userId, "free", Money.Create(0m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), TestUserId, TimeProvider.System);
        cancelled.Cancel(TestUserId, TimeProvider.System);

        repository.Add(cancelled);
        await repository.SaveChangesAsync();

        Subscription? result = await repository.GetActiveByUserIdAsync(userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Update_ModifiesSubscription()
    {
        SubscriptionRepository repository = CreateRepository();
        Subscription subscription = Subscription.Create(TestUserId, "pro", Money.Create(29.99m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), TestUserId, TimeProvider.System);

        repository.Add(subscription);
        await repository.SaveChangesAsync();

        subscription.Cancel(TestUserId, TimeProvider.System);
        repository.Update(subscription);
        await repository.SaveChangesAsync();

        Subscription? result = await repository.GetByIdAsync(subscription.Id);
        result.Should().NotBeNull();
        result.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        SubscriptionRepository repository = CreateRepository();

        Subscription? result = await repository.GetByIdAsync(SubscriptionId.New());

        result.Should().BeNull();
    }
}
