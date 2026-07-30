/**
 * Role and permission membership over the resolved {@link CurrentUser}.
 *
 * These read the TYPED arrays `UsersController.GetCurrentUser` answers with
 * (`roles`, `permissions`) rather than the free-form claim bag the SDK's
 * `claims.ts` helpers walk. Both exist on purpose: the SDK's `hasRole`/`isAdmin`
 * take any `WallowUser` (an OIDC token's claims, where `roles` may arrive as a
 * bare string under `role`), while these take the API's own response shape.
 *
 * They are read-only conveniences for gating UI, NOT an authorization decision —
 * the API re-checks every role and permission on every request.
 */
import type { CurrentUser } from "./current-user";

/**
 * Whether the user holds `role`, compared case-INSENSITIVELY.
 *
 * `ClaimsPrincipalExtensions.GetRoles()` deduplicates with
 * `StringComparer.OrdinalIgnoreCase`, and the SDK's `isAdmin` — re-exported from
 * this package's barrel — compares the same way, so a case-sensitive check here
 * would let `hasRole(user, "admin")` and `isAdmin(user)` disagree about one user.
 *
 * @param user The resolved user, or `null`/`undefined` when anonymous.
 * @param role The role name to look for. A blank name is never held.
 */
export function hasRole(user: CurrentUser | null | undefined, role: string): boolean {
  const wanted: string = role.trim().toLowerCase();
  if (wanted === "") {
    return false;
  }

  return (user?.roles ?? []).some((held: string): boolean => held.toLowerCase() === wanted);
}

/**
 * Whether the user holds `permission`, compared case-SENSITIVELY.
 *
 * `PermissionAuthorizationHandler` decides with a plain
 * `permissions.Contains(requirement.Permission)` — ordinal. A lenient check here
 * would render a control the next request refuses.
 *
 * @param user The resolved user, or `null`/`undefined` when anonymous.
 * @param permission The permission name to look for. A blank name is never held.
 */
export function hasPermission(user: CurrentUser | null | undefined, permission: string): boolean {
  const wanted: string = permission.trim();
  if (wanted === "") {
    return false;
  }

  return (user?.permissions ?? []).includes(wanted);
}
