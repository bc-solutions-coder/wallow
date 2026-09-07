using OpenIddict.EntityFrameworkCore.Models;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Clears tracked state and application token/authorization navigations before deletion.
/// This prevents previously loaded tokens from being reattached after bulk deletion.
/// Callers must save pending changes before clearing the context.
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
