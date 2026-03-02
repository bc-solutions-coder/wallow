using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Billing.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Foundry.Billing.Tests.Infrastructure.Services;

public class SubscriptionQueryServiceTests
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILogger<SubscriptionQueryService> _logger;
    private readonly SubscriptionQueryService _service;

    public SubscriptionQueryServiceTests()
    {
        _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
        _logger = Substitute.For<ILogger<SubscriptionQueryService>>();
        _service = new SubscriptionQueryService(_subscriptionRepository, _logger);
    }

    [Fact]
    public async Task GetActivePlanCodeAsync_WithActiveSubscription_ReturnsPlanName()
    {
        Guid tenantId = Guid.NewGuid();
        Subscription subscription = Subscription.Create(tenantId, "pro", Money.Create(29.99m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), Guid.NewGuid(), TimeProvider.System);

        _subscriptionRepository.GetActiveByUserIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(subscription);

        string? result = await _service.GetActivePlanCodeAsync(tenantId);

        result.Should().Be("pro");
    }

    [Fact]
    public async Task GetActivePlanCodeAsync_WithNoSubscription_ReturnsNull()
    {
        Guid tenantId = Guid.NewGuid();

        _subscriptionRepository.GetActiveByUserIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        string? result = await _service.GetActivePlanCodeAsync(tenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActivePlanCodeAsync_WhenExceptionThrown_ReturnsNull()
    {
        Guid tenantId = Guid.NewGuid();

        _subscriptionRepository.GetActiveByUserIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns<Subscription?>(_ => throw new InvalidOperationException("Database error"));

        string? result = await _service.GetActivePlanCodeAsync(tenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActivePlanCodeAsync_CallsRepositoryWithCorrectTenantId()
    {
        Guid tenantId = Guid.NewGuid();

        _subscriptionRepository.GetActiveByUserIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((Subscription?)null);

        await _service.GetActivePlanCodeAsync(tenantId);

        await _subscriptionRepository.Received(1).GetActiveByUserIdAsync(tenantId, Arg.Any<CancellationToken>());
    }
}
