using Foundry.Shared.Kernel.Domain;
using JetBrains.Annotations;

namespace Foundry.Identity.Domain.Events;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record SsoConfigurationActivatedEvent(
    Guid SsoConfigurationId,
    Guid TenantId,
    string DisplayName,
    string Protocol) : DomainEvent;
