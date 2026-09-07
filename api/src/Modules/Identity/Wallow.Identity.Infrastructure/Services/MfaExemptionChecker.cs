using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Allows exemption only when every active membership has settings that exempt this user.
/// Cookie login may have no selected organization, so checking just one could bypass another
/// organization's requirement. No memberships or missing settings deny exemption.
/// </summary>
public sealed class MfaExemptionChecker : IMfaExemptionChecker
{
    private readonly IdentityDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public MfaExemptionChecker(IdentityDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<bool> IsExemptAsync(WallowUser user, CancellationToken ct)
    {
        // IgnoreQueryFilters throughout: login has no tenant.
        List<OrganizationId> organizationIds = await _dbContext.Memberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == user.Id && m.Status == MembershipStatus.Active)
            .Select(m => m.OrganizationId)
            .ToListAsync(ct);

        if (organizationIds.Count == 0)
        {
            return false;
        }

        List<OrganizationSettings> settings = await _dbContext.OrganizationSettings
            .IgnoreQueryFilters()
            .Where(s => organizationIds.Contains(s.OrganizationId))
            .ToListAsync(ct);

        // An organization with no settings row states no policy, so it cannot state an exemption.
        if (settings.Count != organizationIds.Count)
        {
            return false;
        }

        return settings.TrueForAll(s => ExemptsThisUser(s, user));
    }

    /// <summary>
    /// Accepts passwordless exemption or an unexpired user grace deadline.
    /// Grace applies only when this organization permits a positive grace period.
    /// </summary>
    private bool ExemptsThisUser(OrganizationSettings settings, WallowUser user)
    {
        if (!settings.RequireMfa && settings.AllowPasswordlessLogin && !user.HasPassword)
        {
            return true;
        }

        return settings.MfaGracePeriodDays > 0
            && user.MfaGraceDeadline is not null
            && user.MfaGraceDeadline > _timeProvider.GetUtcNow();
    }
}
