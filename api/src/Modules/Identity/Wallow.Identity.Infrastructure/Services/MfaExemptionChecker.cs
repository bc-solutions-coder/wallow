using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Decides whether a login may skip the MFA challenge. This runs during cookie login, before any
/// client or organization is known, so it cannot consult "the" organization: the answer has to hold
/// for every organization the session can go on to acquire a token for. It therefore takes the
/// STRICTEST policy across every Active membership — one organization that asks for a second factor
/// is enough to ask for it here.
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
    /// Whether this one organization asks nothing of the user. The grace deadline lives on the user
    /// but is granted by an organization that turned MFA on, so only an organization that offers a
    /// grace period may honour it — otherwise one organization's deadline excuses the user from
    /// another organization's requirement.
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
