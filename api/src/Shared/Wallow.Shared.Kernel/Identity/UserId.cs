namespace Wallow.Shared.Kernel.Identity;

public readonly record struct UserId(Guid Value) : IStronglyTypedId<UserId>
{
    public static UserId Create(Guid value) => new(value);
    public static UserId New() => new(Guid.NewGuid());
}
