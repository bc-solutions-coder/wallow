/**
 * Browser-safe barrel for @bc-solutions-coder/auth — the package's only entry
 * point, and the ONE place an app gets its auth surface from.
 *
 * Two kinds of export live here:
 *
 *   1. this package's own current-user layer — the canonical query, the hook, the
 *      `beforeLoad` primer, and the role/permission helpers (`hasRole`,
 *      `hasPermission`, `isAdmin`) over the typed `CurrentUser`;
 *   2. re-exports of the SDK's route guards, so an app's auth imports come from
 *      ONE package instead of being split across two. They are re-exported by
 *      reference, not wrapped: `requireAuth` from here IS the SDK's
 *      `requireAuth`.
 *
 * Nothing in this package imports a router. See `src/use-current-user.ts`.
 */
export { type CurrentUser, currentUserQuery } from "./current-user";
export { useCurrentUser } from "./use-current-user";
export { hasPermission, hasRole, isAdmin } from "./authorization";
export { type EnsureCurrentUserOptions, ensureCurrentUser } from "./ensure-current-user";

export {
  type LoginRedirectOptions,
  type RequireAuthOptions,
  type WallowUser,
  loginRedirect,
  requireAuth,
} from "@bc-solutions-coder/sdk";
