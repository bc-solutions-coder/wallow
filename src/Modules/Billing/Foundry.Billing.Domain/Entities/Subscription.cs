using Foundry.Billing.Domain.Enums;
using Foundry.Billing.Domain.Events;
using Foundry.Billing.Domain.Exceptions;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Billing.Domain.Entities;

/// <summary>
/// Subscription aggregate root. Represents a recurring billing plan for a user.
/// </summary>
/// <remarks>
/// State machine transitions (derived from guard clauses):
/// <code>
///                    Renew()
///                  ┌────────┐
///                  │        │
///   [Create]       ▼        │
///     ──► Active ──┴──┬─────────────► Cancelled
///           │         │  Cancel()        ▲
///           │         │                  │
///           │ MarkPastDue()    Cancel()  │
///           │         │                  │
///           ▼         │                  │
///        Expired ◄────┼──── PastDue ─────┘
///           ▲         │        │
///           │  Expire()        │
///           └──────────────────┘
///                  Expire()
/// </code>
/// </remarks>
public sealed class Subscription : AggregateRoot<SubscriptionId>, ITenantScoped, IHasCustomFields
{
    public TenantId TenantId { get; init; }
    public Guid UserId { get; private set; }
    public string PlanName { get; private set; } = string.Empty;
    public Money Price { get; private set; } = null!;
    public SubscriptionStatus Status { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public DateTime CurrentPeriodStart { get; private set; }
    public DateTime CurrentPeriodEnd { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Dictionary<string, object>? CustomFields { get; private set; }

    public void SetCustomFields(Dictionary<string, object>? customFields)
    {
        CustomFields = customFields;
    }

    private Subscription() { } // EF Core

    private Subscription(
        Guid userId,
        string planName,
        Money price,
        DateTime startDate,
        DateTime periodEnd,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = SubscriptionId.New();
        UserId = userId;
        PlanName = planName;
        Price = price;
        Status = SubscriptionStatus.Active;
        StartDate = startDate;
        CurrentPeriodStart = startDate;
        CurrentPeriodEnd = periodEnd;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

    public static Subscription Create(
        Guid userId,
        string planName,
        Money price,
        DateTime startDate,
        DateTime periodEnd,
        Guid createdByUserId,
        TimeProvider timeProvider,
        Dictionary<string, object>? customFields = null)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleException("Billing.UserIdRequired", "User ID is required");
        }

        if (string.IsNullOrWhiteSpace(planName))
        {
            throw new BusinessRuleException(
                "Billing.PlanNameRequired",
                "Subscription plan name cannot be empty");
        }

        Subscription subscription = new Subscription(userId, planName, price, startDate, periodEnd, createdByUserId, timeProvider);
        subscription.CustomFields = customFields;

        subscription.RaiseDomainEvent(new SubscriptionCreatedDomainEvent(
            subscription.Id.Value,
            userId,
            planName,
            price.Amount,
            price.Currency));

        return subscription;
    }

    public void Renew(DateTime newPeriodEnd, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != SubscriptionStatus.Active)
        {
            throw new InvalidSubscriptionStatusTransitionException(
                Status.ToString(),
                nameof(SubscriptionStatus.Active));
        }

        CurrentPeriodStart = CurrentPeriodEnd;
        CurrentPeriodEnd = newPeriodEnd;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void MarkPastDue(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != SubscriptionStatus.Active)
        {
            throw new InvalidSubscriptionStatusTransitionException(
                Status.ToString(),
                nameof(SubscriptionStatus.PastDue));
        }

        Status = SubscriptionStatus.PastDue;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void Cancel(Guid cancelledByUserId, TimeProvider timeProvider)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            throw new InvalidSubscriptionStatusTransitionException(
                Status.ToString(),
                nameof(SubscriptionStatus.Cancelled));
        }

        Status = SubscriptionStatus.Cancelled;
        CancelledAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow(), cancelledByUserId);

        RaiseDomainEvent(new SubscriptionCancelledDomainEvent(
            Id.Value,
            UserId,
            CancelledAt.Value));
    }

    public void Expire(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.PastDue))
        {
            throw new InvalidSubscriptionStatusTransitionException(
                Status.ToString(),
                nameof(SubscriptionStatus.Expired));
        }

        Status = SubscriptionStatus.Expired;
        EndDate = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }
}
