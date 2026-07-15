// ReSharper disable once CheckNamespace
namespace JetBrains.Annotations;

[AttributeUsage(AttributeTargets.All)]
internal sealed class UsedImplicitlyAttribute(
    ImplicitUseTargetFlags targetFlags = ImplicitUseTargetFlags.Default) : Attribute
{
    public ImplicitUseTargetFlags TargetFlags { get; } = targetFlags;
}

[Flags]
internal enum ImplicitUseTargetFlags
{
    Default = 0,
    Itself = 1,
    Members = 2,
    WithMembers = Itself | Members
}
