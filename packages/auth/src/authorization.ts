/**
 * Read roles and permissions from the current user to control UI visibility. These helpers do not
 * authorize requests; the API checks access independently.
 */
import type { CurrentUser } from "./current-user";

/** The role name the API grants administrators (`RolePermissionMapping`'s key). */
const ADMIN_ROLE: string = "admin";

/**
 * Return whether the user has the named role, ignoring case and trimming the requested name.
 * Anonymous users, missing roles, and blank names return false.
 */
export function hasRole(user: CurrentUser | null | undefined, role: string): boolean {
  const wanted: string = role.trim().toLowerCase();
  if (wanted === "") {
    return false;
  }

  return (user?.roles ?? []).some((held: string): boolean => held.toLowerCase() === wanted);
}

/**
 * Return whether the user has the admin organization role, ignoring case. This does not imply
 * global administrator authority.
 */
export function isAdmin(user: CurrentUser | null | undefined): boolean {
  return hasRole(user, ADMIN_ROLE);
}

/**
 * Return whether the user has the exact permission name after trimming the requested name.
 * Permission matching is case-sensitive; anonymous users and blank names return false.
 */
export function hasPermission(user: CurrentUser | null | undefined, permission: string): boolean {
  const wanted: string = permission.trim();
  if (wanted === "") {
    return false;
  }

  return (user?.permissions ?? []).includes(wanted);
}

/**
 * Return whether the API identifies the user as a global administrator. Organization roles do not
 * imply this authority; anonymous users return false.
 */
export function isGlobalAdmin(user: CurrentUser | null | undefined): boolean {
  return user?.isGlobalAdmin === true;
}
