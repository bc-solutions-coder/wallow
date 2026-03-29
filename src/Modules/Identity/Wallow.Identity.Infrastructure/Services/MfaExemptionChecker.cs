using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

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
        // Grace period: user is exempt if their MFA grace deadline hasn't passed yet
        if (user.MfaGraceDeadline is not null && user.MfaGraceDeadline > _timeProvider.GetUtcNow())
        {
            return true;
        }

        // Find the user's organization membership
        // IgnoreQueryFilters: this runs during login when tenant context is not yet set
        OrganizationMember? membership = await _dbContext.OrganizationMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == user.Id, ct);

        if (membership is null)
        {
            return false;
        }

        // EF shadow property holds the FK — read it via Entry
        OrganizationId organizationId = _dbContext.Entry(membership).Property<OrganizationId>("organization_id").CurrentValue;

        OrganizationSettings? settings = await _dbContext.OrganizationSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

        if (settings is null)
        {
            return false;
        }

        // Exempt if org doesn't require MFA AND allows passwordless AND user has no password
        return !settings.RequireMfa && settings.AllowPasswordlessLogin && !user.HasPassword;
    }
}
