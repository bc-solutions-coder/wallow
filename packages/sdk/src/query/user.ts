/**
 * Current-user query module — the auth-state seam for router `beforeLoad`.
 *
 * Ported from apps/wallow-web/src/lib/wallow-sdk.ts:263-280's `user.me()` slice,
 * keyed off the SHARED `queryKeys.auth.currentUser()` (so wallow-auth's screens
 * reuse ONE current-user query rather than duplicating it).
 *
 * `staleTime: 30_000` holds the resolved user for 30s so `beforeLoad`'s
 * `ensureQueryData` stops refetching on every navigation.
 *
 * SSR DETECTION: the queryFn detects server rendering by the PRESENCE of an SSR
 * request context (`getSsrRequestContext()`), NOT `import.meta.env.SSR` — the
 * SDK is bundler-agnostic and must not read Vite's SSR flag. When a context is
 * present, `getUser()` is pointed at an absolute origin the host can reach ITSELF
 * (`resolveSsrFetchOrigin`, which prefers the context's `internalOrigin` over the
 * browser-facing one — Wallow-spb5) and the session cookie is forwarded (Node's
 * fetch needs both); otherwise the browser's same-origin relative request with
 * the ambient cookie is correct.
 */
import { queryOptions } from "@tanstack/react-query";

import { getUser, type WallowUser } from "../auth";
import { getSsrRequestContext, resolveSsrFetchOrigin, type SsrRequestContext } from "../ssr";
import { ensureQueryBootstrapped } from "./bootstrap";
import { queryKeys } from "./keys";

/** How long the resolved current user is held before `beforeLoad` refetches. */
const CURRENT_USER_STALE_TIME_MS: number = 30_000;

/**
 * queryOptions factory for the signed-in user, keyed
 * `queryKeys.auth.currentUser()`. Resolves `null` when anonymous.
 */
export const userQueries = {
  currentUser: () =>
    queryOptions({
      queryKey: queryKeys.auth.currentUser(),
      queryFn: (): Promise<WallowUser | null> => {
        ensureQueryBootstrapped();
        const context: SsrRequestContext | undefined = getSsrRequestContext();
        if (context !== undefined) {
          return getUser({
            baseUrl: resolveSsrFetchOrigin(context),
            ...(context.cookie !== undefined ? { headers: { cookie: context.cookie } } : {}),
          });
        }
        return getUser();
      },
      staleTime: CURRENT_USER_STALE_TIME_MS,
    }),
};
