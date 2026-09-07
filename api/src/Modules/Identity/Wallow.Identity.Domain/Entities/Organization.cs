using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// An organization defines a tenant. <see cref="Create"/> derives TenantId from the
/// new organization ID, maintaining <c>Id.Value == TenantId.Value</c>.
/// Organization membership and roles belong to <see cref="Membership"/>.
/// </summary>
public sealed class Organization : AggregateRoot<OrganizationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public Guid? ArchivedBy { get; private set; }

    public DateTimeOffset? PlatformSuspendedAt { get; private set; }
    public Guid? PlatformSuspendedBy { get; private set; }
    public string? PlatformSuspensionReason { get; private set; }

    /// <summary>
    /// Platform suspension is independent of <see cref="IsActive"/>; changing the
    /// organization's own active state does not clear it.
    /// </summary>
    public bool IsPlatformSuspended => PlatformSuspendedAt is not null;

    // ReSharper disable once UnusedMember.Local
    private Organization() { } // EF Core

    private Organization(
        string name,
        string slug,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = OrganizationId.New();

        TenantId = TenantId.Create(Id.Value);
        Name = name;
        Slug = slug;
        IsActive = true;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

#pragma warning disable IDE0060, RCS1163 // tenantId is ignored; creation derives it from the new organization ID.
    public static Organization Create(
        TenantId tenantId,
        string name,
        string slug,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException(IdentityErrors.OrganizationNameRequired);
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new BusinessRuleException(IdentityErrors.OrganizationSlugRequired);
        }

        return new Organization(name, slug, createdByUserId, timeProvider);
    }
#pragma warning restore IDE0060, RCS1163

    public void Archive(Guid actorId, TimeProvider timeProvider)
    {
        if (!IsActive)
        {
            throw new BusinessRuleException(IdentityErrors.OrganizationAlreadyInactive);
        }

        IsActive = false;
        ArchivedAt = timeProvider.GetUtcNow();
        ArchivedBy = actorId;
        SetUpdated(timeProvider.GetUtcNow(), actorId);
    }

    public void Reactivate(Guid actorId, TimeProvider timeProvider)
    {
        if (IsActive)
        {
            throw new BusinessRuleException(IdentityErrors.OrganizationAlreadyActive);
        }

        IsActive = true;
        ArchivedAt = null;
        ArchivedBy = null;
        SetUpdated(timeProvider.GetUtcNow(), actorId);
    }

    /// <summary>
    /// Records the platform suspension reason, actor, and time.
    /// </summary>
    public void SuspendByPlatform(string reason, Guid actorId, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException(IdentityErrors.PlatformSuspensionReasonRequired);
        }

        if (IsPlatformSuspended)
        {
            throw new BusinessRuleException(IdentityErrors.OrganizationAlreadySuspendedByPlatform);
        }

        PlatformSuspendedAt = timeProvider.GetUtcNow();
        PlatformSuspendedBy = actorId;
        PlatformSuspensionReason = reason;
        SetUpdated(timeProvider.GetUtcNow(), actorId);
    }

    public void ReinstateByPlatform(Guid actorId, TimeProvider timeProvider)
    {
        if (!IsPlatformSuspended)
        {
            throw new BusinessRuleException(IdentityErrors.OrganizationNotSuspendedByPlatform);
        }

        PlatformSuspendedAt = null;
        PlatformSuspendedBy = null;
        PlatformSuspensionReason = null;
        SetUpdated(timeProvider.GetUtcNow(), actorId);
    }

    /// <summary>
    /// Requires a platform operator to delete an organization under platform suspension.
    /// </summary>
    public void EnsureDeletable(bool byPlatformOperator)
    {
        if (IsPlatformSuspended && !byPlatformOperator)
        {
            throw new BusinessRuleException(IdentityErrors.OrganizationSuspendedByPlatform, "A platform-suspended organization can only be deleted by the platform");
        }
    }

    public static void ConfirmNameForDeletion(Organization org, string confirmedName)
    {
        if (confirmedName != org.Name)
        {
            throw new BusinessRuleException(IdentityErrors.OrganizationNameMismatch);
        }
    }
}
