/**
 * Apps feature query layer (Wallow-8w1h.5.1) — copies the CANONICAL
 * Organizations `api.ts` template (Wallow-8w1h.4.1). `api.ts` is the ONLY layer
 * route/component files import for data: it exposes TanStack Query
 * `queryOptions` factories and mutation factories, all delegating to
 * `getWallowSdk().apps` (never importing generated SDK ops directly; that is the
 * facade's job).
 *
 * `list()` is keyed `['apps']`; `detail(clientId)` is keyed `['apps', clientId]`,
 * each queryFn delegates to the facade `apps` slice, and
 * `registerAppMutation` invalidates the `['apps']` list on success.
 */
import { queryOptions, type QueryClient } from "@tanstack/react-query";

import { getWallowSdk } from "../../lib/wallow-sdk";

/**
 * queryOptions factories for the apps list and a single app's detail. `list()`
 * is keyed `['apps']`; `detail(clientId)` is keyed `['apps', clientId]` so a
 * single `invalidateQueries({ queryKey: ['apps'] })` sweeps the feature's cache.
 */
export const appsQueries = {
  list: () =>
    queryOptions({
      queryKey: ["apps"] as const,
      queryFn: () => getWallowSdk().apps.list(),
    }),
  detail: (clientId: string) =>
    queryOptions({
      queryKey: ["apps", clientId] as const,
      queryFn: () => getWallowSdk().apps.get(clientId),
    }),
};

/**
 * The register-app request body (mirrors the API `RegisterAppRequest`). Note the
 * field remap: DisplayName -> clientName, Scopes -> requestedScopes.
 */
export interface RegisterAppBody {
  clientName: string;
  requestedScopes: string[];
  clientType?: string | null;
  redirectUris?: string[] | null;
}

/**
 * Mutation factory for registering an app. Takes the router/context
 * `QueryClient` so its `onSuccess` can invalidate the `['apps']` list query,
 * keeping the register form free of cache wiring.
 */
export const registerAppMutation = (queryClient: QueryClient) => ({
  mutationFn: (body: RegisterAppBody): Promise<unknown> => getWallowSdk().apps.register(body),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["apps"] });
  },
});
