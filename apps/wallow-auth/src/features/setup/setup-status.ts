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
  try {
    const status = await options.queryClient.ensureQueryData({
      ...setupGetStatusOptions({ client: options.client }),
      staleTime: SETUP_STATUS_STALE_TIME_MS,
    });

    return status.setupRequired;
  } catch {
    return null;
  }
}
