/**
 * Current-user queries and UI access checks, plus the SDK login URL and authentication guard.
 * This entry is browser-safe and does not depend on a router.
 */
export { type CurrentUser, currentUserQuery } from "./current-user";
export { useCurrentUser } from "./use-current-user";
export { hasPermission, hasRole, isAdmin, isGlobalAdmin } from "./authorization";
export { type EnsureCurrentUserOptions, ensureCurrentUser } from "./ensure-current-user";

export {
  type LoginRedirectOptions,
  type RequireAuthOptions,
  type WallowUser,
  loginRedirect,
  requireAuth,
} from "@bc-solutions-coder/sdk";
