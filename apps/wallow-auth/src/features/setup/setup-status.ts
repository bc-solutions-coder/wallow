/**
 * The `beforeLoad` half of the first-run contract, shaped after
 * `@bc-solutions-coder/auth`'s `ensureCurrentUser`: resolve "is setup still
 * required?" into the request's query cache before a route renders, so the two
 * gates that read it (`/setup` and `/login`) share one answer per request.
 *
 * `ensureQueryData` plus the `staleTime` below make a gate on every navigation
 * a cache read instead of a request per route change.
 */
import type { QueryClient } from "@bc-solutions-coder/query";
import type { WallowSdk } from "@bc-solutions-coder/sdk";

import { setupGetStatusOptions } from "./api";

/** Options for {@link ensureSetupRequired}. */
export interface EnsureSetupRequiredOptions {
  /** The request's query cache, off the router context — never a module-global one. */
  readonly queryClient: QueryClient;
  /** The request-scoped client from `createWallowSdk()` (`context.sdk.client`). */
  readonly client: WallowSdk["client"];
}

/** How long one answer serves repeated gates — matches `currentUserQuery`'s. */
const SETUP_STATUS_STALE_TIME_MS = 30_000;

/** What `GET /v1/identity/setup/status` says, as the two gates consume it. */
export interface SetupStatus {
  readonly setupRequired: boolean;
  /**
   * The organization the seed already created — the one the dashboard client
   * is bound to — offered while setup is open so the administrator joins it
   * rather than founding a sibling no client points at. `undefined` when there
   * is none, or more than one, to offer.
   */
  readonly seededOrganizationName: string | undefined;
}

/**
 * The full setup status, or `null` when the API could not say. Same contract
 * as {@link ensureSetupRequired}, which is this with the organization dropped.
 */
export async function ensureSetupStatus(
  options: EnsureSetupRequiredOptions,
): Promise<SetupStatus | null> {
  try {
    const status = await options.queryClient.ensureQueryData({
      ...setupGetStatusOptions({ client: options.client }),
      staleTime: SETUP_STATUS_STALE_TIME_MS,
    });

    const name: string | null | undefined = status.organizationName;
    return {
      setupRequired: status.setupRequired,
      seededOrganizationName: name === null || name === undefined || name === "" ? undefined : name,
    };
  } catch {
    return null;
  }
}

/**
 * Whether first-run setup is still required, or `null` when the API could not
 * say (unreachable, or an unexpected rejection).
 *
 * `null` rather than a thrown error is the contract: BOTH callers redirect only
 * on a definite answer — `/setup` leaves its form up unless setup is known
 * complete, `/login` stays a login page unless setup is known open — so a
 * status failure must never take either page down with it.
 */
export async function ensureSetupRequired(
  options: EnsureSetupRequiredOptions,
): Promise<boolean | null> {
  const status: SetupStatus | null = await ensureSetupStatus(options);
  return status === null ? null : status.setupRequired;
}
