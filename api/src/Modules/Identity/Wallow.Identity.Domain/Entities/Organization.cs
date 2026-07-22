using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Domain.Entities;

/// <summary>
/// An organization IS the tenant: <see cref="Create"/> is the only place a tenant id is
/// minted, and it mints the tenant id from the freshly generated organization id so that
/// <c>Id.Value == TenantId.Value</c> by construction. Nothing else may mint a tenant id.
/// </summary>
public sealed class Organization : AggregateRoot<OrganizationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public Guid? ArchivedBy { get; private set; }

    private readonly List<OrganizationMember> _members = [];
    public IReadOnlyList<OrganizationMember> Members => _members.AsReadOnly();

    // ReSharper disable once UnusedMember.Local
    private Organization() { } // EF Core

    private Organization(
        string name,
        string slug,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = OrganizationId.New();
        // The org IS the tenant: mint the tenant id from the freshly generated org id.
        TenantId = TenantId.Create(Id.Value);
        Name = name;
        Slug = slug;
        IsActive = true;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

#pragma warning disable IDE0060, RCS1163 // tenantId retained for call-site compatibility; the tenant id is minted from the new org id so Id == TenantId
    public static Organization Create(
        TenantId tenantId,
        string name,
        string slug,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException(
                "Identity.OrganizationNameRequired",
                "Organization name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new BusinessRuleException(
                "Identity.OrganizationSlugRequired",
                "Organization slug cannot be empty");
        }

        return new Organization(name, slug, createdByUserId, timeProvider);
    }
#pragma warning restore IDE0060, RCS1163

    public void AddMember(Guid userId, OrgMemberRole role, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (_members.Any(m => m.UserId == userId))
        {
            throw new BusinessRuleException(
                "Identity.MemberAlreadyExists",
                "User is already a member of this organization");
        }

        OrganizationMember member = new(userId, role);
        _members.Add(member);
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void RemoveMember(Guid userId, Guid updatedByUserId, TimeProvider timeProvider)
    {
        OrganizationMember? member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            throw new BusinessRuleException(
                "Identity.MemberNotFound",
                "User is not a member of this organization");
        }

        _members.Remove(member);
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void Archive(Guid actorId, TimeProvider timeProvider)
    {
        if (!IsActive)
        {
            throw new BusinessRuleException(
                "Identity.OrganizationAlreadyInactive",
                "Organization is already inactive");
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
            throw new BusinessRuleException(
                "Identity.OrganizationAlreadyActive",
                "Organization is already active");
        }

        IsActive = true;
        ArchivedAt = null;
        ArchivedBy = null;
        SetUpdated(timeProvider.GetUtcNow(), actorId);
    }

    public static void ConfirmNameForDeletion(Organization org, string confirmedName)
    {
        if (confirmedName != org.Name)
        {
            throw new BusinessRuleException(
                "Identity.OrganizationNameMismatch",
                "The confirmed name does not match the organization name");
        }
    }
}
