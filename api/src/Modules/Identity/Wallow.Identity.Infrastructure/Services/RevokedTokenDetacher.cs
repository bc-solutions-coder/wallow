using OpenIddict.EntityFrameworkCore.Models;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Revocation loads a client's tokens into the context, and EF fixes them up onto the
/// application's navigations. OpenIddict's delete removes tokens and authorizations in SQL
/// first and then attaches the application graph, so any token still hanging off it is
/// updated as an orphan of a row that no longer exists and reported as a concurrency
/// conflict. Hand the delete a bare application: nothing tracked, nothing reachable.
/// </summary>
internal static class RevokedTokenDetacher
{
    internal static void DetachRevokedTokens(IdentityDbContext dbContext, object application)
    {
        dbContext.ChangeTracker.Clear();
        if (application is OpenIddictEntityFrameworkCoreApplication<Guid> entity)
        {
            entity.Authorizations.Clear();
            entity.Tokens.Clear();
        }
    }
}
