using Wallow.Shared.Kernel.Identity;

namespace Wallow.Billing.Domain.Identity;

public readonly record struct SubscriptionId(Guid Value) : IStronglyTypedId<SubscriptionId>
{
    public static SubscriptionId Create(Guid value) => new(value);
    public static SubscriptionId New() => new(Guid.NewGuid());
}
