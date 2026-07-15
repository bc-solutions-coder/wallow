namespace Wallow.Shared.Kernel.Identity;

public readonly record struct TenantId(Guid Value) : IStronglyTypedId<TenantId>
{
    public static readonly TenantId Platform = new(new Guid("00000000-0000-0000-0000-000000000001"));

    public static TenantId Create(Guid value) => new(value);
    public static TenantId New() => new(Guid.NewGuid());
}
