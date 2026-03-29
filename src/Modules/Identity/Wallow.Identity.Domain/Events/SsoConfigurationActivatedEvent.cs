using JetBrains.Annotations;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Domain.Events;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record SsoConfigurationActivatedEvent(
    Guid SsoConfigurationId,
    Guid TenantId,
    string DisplayName,
    string Protocol) : DomainEvent;
