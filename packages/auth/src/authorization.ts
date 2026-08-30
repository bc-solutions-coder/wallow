/**
 * Role and permission membership over the resolved {@link CurrentUser}.
 *
 * These read the TYPED arrays `UsersController.GetCurrentUser` answers with
 * (`roles`, `permissions`) — the ONE user model at the app boundary. The SDK's
 * browser claim-bag readers that used to sit beside these (a second `hasRole`
 * over a free-form `WallowUser`) are deleted (Wallow-j7qk); OIDC claim decoding
 * is internal to the SDK's server entry (`server/claims.ts`) and never an app
 * concern.
 *
 * They are read-only conveniences for gating UI, NOT an authorization decision —
 * the API re-checks every role and permission on every request.
 */
import type { CurrentUser } from "./current-user";

/** The role name the API grants administrators (`RolePermissionMapping`'s key). */
const ADMIN_ROLE: string = "admin";

/**
 * Whether the user holds `role`, compared case-INSENSITIVELY.
 *
 * `ClaimsPrincipalExtensions.GetRoles()` deduplicates with
 * `StringComparer.OrdinalIgnoreCase`, so a case-sensitive check here would hide
 * a control from a user the API would let through. {@link isAdmin} is defined
 * over this function, so the two agree about any user by construction.
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
 * Whether the user holds the `admin` role — {@link hasRole} with the one role
 * name the apps actually gate on, so call sites read as intent rather than a
 * string literal.
 *
 * @param user The resolved user, or `null`/`undefined` when anonymous.
 */
export function isAdmin(user: CurrentUser | null | undefined): boolean {
  return hasRole(user, ADMIN_ROLE);
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

/**
 * Whether the user holds the platform operator's own authority — the
 * `is_global_admin` claim `GetCurrentUser` surfaces as `isGlobalAdmin`. The
 * authority is minted at sign-in and never derived from organization roles, so
 * neither {@link hasRole} nor {@link isAdmin} implies it.
 *
 * @param user The resolved user, or `null`/`undefined` when anonymous.
 */
export function isGlobalAdmin(user: CurrentUser | null | undefined): boolean {
  return user?.isGlobalAdmin === true;
}
