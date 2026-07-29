using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wallow.Identity.Infrastructure.Authorization;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// AdminAccess and SystemSettings are tenant-scoped for an ordinary admin: the role grants
/// them inside the caller's own tenant only, so a request resolved onto a DIFFERENT tenant
/// must not carry them. The non-assignable global-admin claim is the single cross-tenant
/// escape hatch and grants them on its own, in every tenant, without any role.
///
/// TenantResolutionMiddleware runs before this middleware (Wallow.Api/Program.cs) and stamps
/// the resolved tenant onto HttpContext.Items["TenantId"]; the caller's own tenant is the
/// org_id claim.
/// </summary>
public sealed class PermissionExpansionMiddlewareGlobalAdminTests
{
    private const string TenantItemKey = "TenantId";

    [Fact]
    public async Task InvokeAsync_TenantAdminOnOwnTenant_GetsAdminAccessAndSystemSettings()
    {
        Guid ownTenantId = Guid.NewGuid();
        DefaultHttpContext context = CreateContext(
            ownTenantId,
            ownTenantId,
            new Claim("role", "admin"));

        await InvokeAsync(context);

        List<string> permissions = PermissionsOf(context);
        permissions.Should().Contain(PermissionType.AdminAccess);
        permissions.Should().Contain(PermissionType.SystemSettings);
    }

    [Fact]
    public async Task InvokeAsync_TenantAdminOnAnotherTenant_DoesNotGetAdminAccess()
    {
        DefaultHttpContext context = CreateContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Claim("role", "admin"));

        await InvokeAsync(context);

        PermissionsOf(context).Should().NotContain(
            PermissionType.AdminAccess,
            "an assignable admin role is scoped to the tenant that granted it");
    }

    [Fact]
    public async Task InvokeAsync_TenantAdminOnAnotherTenant_DoesNotGetSystemSettings()
    {
        DefaultHttpContext context = CreateContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Claim("role", "admin"));

        await InvokeAsync(context);

        PermissionsOf(context).Should().NotContain(
            PermissionType.SystemSettings,
            "an assignable admin role is scoped to the tenant that granted it");
    }

    [Fact]
    public async Task InvokeAsync_GlobalAdminWithoutAnyRole_GetsAdminAccessAndSystemSettings()
    {
        Guid ownTenantId = Guid.NewGuid();
        DefaultHttpContext context = CreateContext(
            ownTenantId,
            ownTenantId,
            new Claim(ClaimsPrincipalExtensions.GlobalAdminClaimType, "true"));

        await InvokeAsync(context);

        List<string> permissions = PermissionsOf(context);
        permissions.Should().Contain(
            PermissionType.AdminAccess,
            "the global-admin claim governs on its own; it is not backed by an assignable role");
        permissions.Should().Contain(PermissionType.SystemSettings);
    }

    [Fact]
    public async Task InvokeAsync_GlobalAdminOnAnotherTenant_KeepsAdminAccessAndSystemSettings()
    {
        DefaultHttpContext context = CreateContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Claim("role", "admin"),
            new Claim(ClaimsPrincipalExtensions.GlobalAdminClaimType, "true"));

        await InvokeAsync(context);

        List<string> permissions = PermissionsOf(context);
        permissions.Should().Contain(
            PermissionType.AdminAccess,
            "tenant-scoping ordinary admins must not lock the global admin out of other tenants");
        permissions.Should().Contain(PermissionType.SystemSettings);
    }

    [Fact]
    public async Task InvokeAsync_GlobalAdminClaimFalseOnAnotherTenant_DoesNotGetAdminAccess()
    {
        DefaultHttpContext context = CreateContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Claim("role", "admin"),
            new Claim(ClaimsPrincipalExtensions.GlobalAdminClaimType, "false"));

        await InvokeAsync(context);

        PermissionsOf(context).Should().NotContain(
            PermissionType.AdminAccess,
            "the claim value must be honoured, not merely its presence");
    }

    private static async Task InvokeAsync(DefaultHttpContext context)
    {
        PermissionExpansionMiddleware middleware = new(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);
    }

    private static DefaultHttpContext CreateContext(Guid ownTenantId, Guid resolvedTenantId, params Claim[] claims)
    {
        Claim[] allClaims =
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("org_id", ownTenantId.ToString()),
            .. claims,
        ];

        DefaultHttpContext context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(allClaims, "test")),
        };
        context.Items[TenantItemKey] = resolvedTenantId.ToString();
        return context;
    }

    private static List<string> PermissionsOf(HttpContext context) =>
        context.User.FindAll("permission").Select(c => c.Value).ToList();
}
