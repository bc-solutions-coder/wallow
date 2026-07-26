/**
 * Apps query module — self-service OAuth client registration.
 *
 * Ported from apps/wallow-web/src/features/apps/api.ts (op-to-call mapping at
 * apps/wallow-web/src/lib/wallow-sdk.ts:248-262) with the canonical template's
 * three changes: (a) every queryKey comes from `queryKeys.apps.*`; (b) every
 * queryFn/mutationFn starts with `ensureQueryBootstrapped()` then calls the
 * generated op directly via `unwrap(...)`; (c) the request-body interfaces live
 * and are exported here.
 */
import { queryOptions, type QueryClient } from "@tanstack/react-query";

import { unwrap } from "../facade";
import {
  getV1IdentityApps,
  getV1IdentityAppsByClientId,
  postV1IdentityAppsByClientIdBranding,
  postV1IdentityAppsRegister,
} from "../generated";
import { ensureQueryBootstrapped } from "./bootstrap";
import { queryKeys } from "./keys";

/**
 * queryOptions factories for the apps list and a single app's detail. `list()`
 * is keyed `queryKeys.apps.all`; `detail(clientId)` is keyed
 * `queryKeys.apps.detail(clientId)` so a single
 * `invalidateQueries({ queryKey: queryKeys.apps.all })` sweeps the feature's cache.
 */
export const appsQueries = {
  list: () =>
    queryOptions({
      queryKey: queryKeys.apps.all,
      queryFn: () => {
        ensureQueryBootstrapped();
        return unwrap(getV1IdentityApps());
      },
    }),
  detail: (clientId: string) =>
    queryOptions({
      queryKey: queryKeys.apps.detail(clientId),
      queryFn: () => {
        ensureQueryBootstrapped();
        return unwrap(getV1IdentityAppsByClientId({ path: { clientId } }));
      },
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
  postLogoutRedirectUris?: string[] | null;
}

/**
 * Mutation factory for registering an app. Takes the router/context
 * `QueryClient` so its `onSuccess` invalidates the apps list query, keeping the
 * register form free of cache wiring.
 */
export const registerAppMutation = (queryClient: QueryClient) => ({
  mutationFn: (body: RegisterAppBody): Promise<unknown> => {
    ensureQueryBootstrapped();
    return unwrap(postV1IdentityAppsRegister({ body }));
  },
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.apps.all });
  },
});

/**
 * The upsert-branding request body: optional display name, tagline, and logo
 * file. It maps onto the API's multipart form via
 * `postV1IdentityAppsByClientIdBranding` (the DisplayName/Tagline field casing).
 */
export interface UpsertBrandingBody {
  displayName?: string;
  tagline?: string;
  logo?: File;
}

/**
 * Upsert an app's optional branding for `clientId`; invalidates that app's detail
 * query on success so the app card refreshes.
 */
export const upsertBrandingMutation = (queryClient: QueryClient, clientId: string) => ({
  mutationFn: (body: UpsertBrandingBody): Promise<unknown> => {
    ensureQueryBootstrapped();
    return unwrap(
      postV1IdentityAppsByClientIdBranding({
        path: { clientId },
        body: { DisplayName: body.displayName, Tagline: body.tagline, logo: body.logo },
      }),
    );
  },
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.apps.detail(clientId) });
  },
});
