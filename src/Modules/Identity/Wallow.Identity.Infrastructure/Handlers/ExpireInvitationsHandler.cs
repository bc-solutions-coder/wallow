using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Handlers;

public sealed record ExpireInvitationsMessage;

public static class ExpireInvitationsHandler
{
    public static async Task Handle(ExpireInvitationsMessage _, IdentityDbContext dbContext, TimeProvider timeProvider)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        List<Invitation> expiredInvitations = await dbContext.Invitations
            .AsTracking()
            .Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAt <= now)
            .ToListAsync();

        foreach (Invitation invitation in expiredInvitations)
        {
            invitation.MarkExpired();
        }

        if (expiredInvitations.Count > 0)
        {
            await dbContext.SaveChangesAsync();
        }
    }
}
