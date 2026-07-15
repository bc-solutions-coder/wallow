using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.Settings;

public sealed record TenantSettingChangedEvent(TenantId TenantId, string Key, string? ModuleId) : DomainEvent;

public sealed record UserSettingChangedEvent(UserId UserId, TenantId TenantId, string Key, string? ModuleId) : DomainEvent;
