using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Enums;
using Wallow.Billing.Domain.Events;
using Wallow.Billing.Domain.Exceptions;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Kernel.Domain;
using static Wallow.Billing.Tests.Domain.Entities.SubscriptionTestHelpers;

namespace Wallow.Billing.Tests.Domain.Entities;

public class SubscriptionCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsSubscriptionInActiveStatus()
    {
        Guid userId = Guid.NewGuid();
        string planName = "Pro Plan";
        Money price = Money.Create(29.99m, "USD");
        DateTime startDate = DateTime.UtcNow;
        DateTime periodEnd = startDate.AddMonths(1);
        Guid createdBy = Guid.NewGuid();

        Subscription subscription = Subscription.Create(userId, planName, price, startDate, periodEnd, createdBy, TimeProvider.System);

        subscription.UserId.Should().Be(userId);
        subscription.PlanName.Should().Be(planName);
        subscription.Price.Should().Be(price);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.StartDate.Should().Be(startDate);
        subscription.CurrentPeriodStart.Should().Be(startDate);
        subscription.CurrentPeriodEnd.Should().Be(periodEnd);
        subscription.EndDate.Should().BeNull();
        subscription.CancelledAt.Should().BeNull();
    }

    [Fact]
    public void Create_RaisesSubscriptionCreatedDomainEvent()
    {
        Guid userId = Guid.NewGuid();
        string planName = "Enterprise Plan";
        Money price = Money.Create(99.99m, "EUR");
        DateTime startDate = DateTime.UtcNow;
        DateTime periodEnd = startDate.AddMonths(1);

        Subscription subscription = Subscription.Create(userId, planName, price, startDate, periodEnd, Guid.NewGuid(), TimeProvider.System);

        subscription.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SubscriptionCreatedDomainEvent>()
            .Which.Should().Match<SubscriptionCreatedDomainEvent>(e =>
                e.SubscriptionId == subscription.Id.Value &&
                e.UserId == userId &&
                e.PlanName == planName &&
                e.Amount == 99.99m &&
                e.Currency == "EUR");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyPlanName_ThrowsBusinessRuleException(string? planName)
    {
        Func<Subscription> act = () => Subscription.Create(Guid.NewGuid(), planName!, Money.Create(50, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.Code == "Billing.PlanNameRequired");
    }

    [Fact]
    public void Create_WithCustomFields_SetsCustomFields()
    {
        Dictionary<string, object> customFields = new Dictionary<string, object>
        {
            { "paymentProvider", "stripe" },
            { "subscriptionId", "sub_123" }
        };

        Subscription subscription = Subscription.Create(Guid.NewGuid(), "Basic Plan", Money.Create(9.99m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), Guid.NewGuid(), TimeProvider.System, customFields);

        subscription.CustomFields.Should().NotBeNull();
        subscription.CustomFields.Should().ContainKey("paymentProvider");
        subscription.CustomFields!["paymentProvider"].Should().Be("stripe");
    }
}

public class SubscriptionRenewTests
{
    [Fact]
    public void Renew_ActiveSubscription_UpdatesPeriodDates()
    {
        Subscription subscription = CreateActiveSubscription();
        DateTime originalPeriodEnd = subscription.CurrentPeriodEnd;
        DateTime newPeriodEnd = originalPeriodEnd.AddMonths(1);
        Guid updatedBy = Guid.NewGuid();

        subscription.Renew(newPeriodEnd, updatedBy, TimeProvider.System);

        subscription.CurrentPeriodStart.Should().Be(originalPeriodEnd);
        subscription.CurrentPeriodEnd.Should().Be(newPeriodEnd);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public void Renew_PastDueSubscription_ThrowsInvalidSubscriptionStatusTransitionException()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.MarkPastDue(Guid.NewGuid(), TimeProvider.System);

        Action act = () => subscription.Renew(DateTime.UtcNow.AddMonths(1), Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidSubscriptionStatusTransitionException>();
    }

    [Fact]
    public void Renew_CancelledSubscription_ThrowsInvalidSubscriptionStatusTransitionException()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.Cancel(Guid.NewGuid(), TimeProvider.System);

        Action act = () => subscription.Renew(DateTime.UtcNow.AddMonths(1), Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidSubscriptionStatusTransitionException>();
    }

    [Fact]
    public void Renew_ExpiredSubscription_ThrowsInvalidSubscriptionStatusTransitionException()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.Expire(Guid.NewGuid(), TimeProvider.System);

        Action act = () => subscription.Renew(DateTime.UtcNow.AddMonths(1), Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidSubscriptionStatusTransitionException>();
    }
}

public class SubscriptionMarkPastDueTests
{
    [Fact]
    public void MarkPastDue_ActiveSubscription_ChangesStatusToPastDue()
    {
        Subscription subscription = CreateActiveSubscription();
        Guid updatedBy = Guid.NewGuid();

        subscription.MarkPastDue(updatedBy, TimeProvider.System);

        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
    }

    [Fact]
    public void MarkPastDue_PastDueSubscription_ThrowsInvalidSubscriptionStatusTransitionException()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.MarkPastDue(Guid.NewGuid(), TimeProvider.System);

        Action act = () => subscription.MarkPastDue(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidSubscriptionStatusTransitionException>();
    }

    [Fact]
    public void MarkPastDue_CancelledSubscription_ThrowsInvalidSubscriptionStatusTransitionException()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.Cancel(Guid.NewGuid(), TimeProvider.System);

        Action act = () => subscription.MarkPastDue(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidSubscriptionStatusTransitionException>();
    }

    [Fact]
    public void MarkPastDue_ExpiredSubscription_ThrowsInvalidSubscriptionStatusTransitionException()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.Expire(Guid.NewGuid(), TimeProvider.System);

        Action act = () => subscription.MarkPastDue(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidSubscriptionStatusTransitionException>();
    }
}

public class SubscriptionCancelTests
{
    [Fact]
    public void Cancel_ActiveSubscription_ChangesStatusToCancelled()
    {
        Subscription subscription = CreateActiveSubscription();
        Guid cancelledBy = Guid.NewGuid();
        DateTime beforeCancel = DateTime.UtcNow;

        subscription.Cancel(cancelledBy, TimeProvider.System);

        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
        subscription.CancelledAt.Should().NotBeNull();
        subscription.CancelledAt.Should().BeOnOrAfter(beforeCancel);
    }

    [Fact]
    public void Cancel_PastDueSubscription_ChangesStatusToCancelled()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.MarkPastDue(Guid.NewGuid(), TimeProvider.System);

        subscription.Cancel(Guid.NewGuid(), TimeProvider.System);

        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void Cancel_RaisesSubscriptionCancelledDomainEvent()
    {
        Subscription subscription = CreateActiveSubscription();
        Guid cancelledBy = Guid.NewGuid();

        subscription.Cancel(cancelledBy, TimeProvider.System);

        subscription.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SubscriptionCancelledDomainEvent>()
            .Which.Should().Match<SubscriptionCancelledDomainEvent>(e =>
                e.SubscriptionId == subscription.Id.Value &&
                e.UserId == subscription.UserId &&
                e.CancelledAt == subscription.CancelledAt!.Value);
    }

    [Fact]
    public void Cancel_CancelledSubscription_ThrowsInvalidSubscriptionStatusTransitionException()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.Cancel(Guid.NewGuid(), TimeProvider.System);

        Action act = () => subscription.Cancel(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidSubscriptionStatusTransitionException>();
    }

    [Fact]
    public void Cancel_ExpiredSubscription_ThrowsInvalidSubscriptionStatusTransitionException()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.Expire(Guid.NewGuid(), TimeProvider.System);

        Action act = () => subscription.Cancel(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidSubscriptionStatusTransitionException>();
    }
}

public class SubscriptionExpireTests
{
    [Fact]
    public void Expire_ActiveSubscription_ChangesStatusToExpired()
    {
        Subscription subscription = CreateActiveSubscription();
        Guid updatedBy = Guid.NewGuid();
        DateTime beforeExpire = DateTime.UtcNow;

        subscription.Expire(updatedBy, TimeProvider.System);

        subscription.Status.Should().Be(SubscriptionStatus.Expired);
        subscription.EndDate.Should().NotBeNull();
        subscription.EndDate.Should().BeOnOrAfter(beforeExpire);
    }

    [Fact]
    public void Expire_PastDueSubscription_ChangesStatusToExpired()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.MarkPastDue(Guid.NewGuid(), TimeProvider.System);

        subscription.Expire(Guid.NewGuid(), TimeProvider.System);

        subscription.Status.Should().Be(SubscriptionStatus.Expired);
    }

    [Fact]
    public void Expire_CancelledSubscription_ThrowsInvalidSubscriptionStatusTransitionException()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.Cancel(Guid.NewGuid(), TimeProvider.System);

        Action act = () => subscription.Expire(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidSubscriptionStatusTransitionException>();
    }

    [Fact]
    public void Expire_ExpiredSubscription_ThrowsInvalidSubscriptionStatusTransitionException()
    {
        Subscription subscription = CreateActiveSubscription();
        subscription.Expire(Guid.NewGuid(), TimeProvider.System);

        Action act = () => subscription.Expire(Guid.NewGuid(), TimeProvider.System);

        act.Should().Throw<InvalidSubscriptionStatusTransitionException>();
    }
}

internal static class SubscriptionTestHelpers
{
    public static Subscription CreateActiveSubscription()
    {
        Subscription subscription = Subscription.Create(Guid.NewGuid(), "Test Plan", Money.Create(19.99m, "USD"), DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), Guid.NewGuid(), TimeProvider.System);
        subscription.ClearDomainEvents();
        return subscription;
    }
}
