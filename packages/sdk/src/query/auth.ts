/**
 * Auth-flow query module — wallow-auth's read-only flow queries.
 *
 * These wrap the wallow-auth screens' read-only lookups; the app's mutations end
 * in navigation rather than cache updates, so this module ships QUERIES ONLY (no
 * mutation/invalidation factories). `currentUser` is intentionally ABSENT here —
 * it lives in `./user` so both apps share ONE current-user query.
 *
 * Each query delegates to the SDK's typed {@link AuthClient}
 * (`createAuthClient()`), built LAZILY on first use (same pattern as `./mfa`) so
 * nothing touches the shared `@hey-api` client before the app registers its
 * bootstrap configurator. Delegating inherits the auth client's
 * WallowError-throwing envelope semantics for free — no `unwrap` import here.
 *
 * Method-name mapping (confirmed against ../auth-client): externalProviders ->
 * getExternalProviders(); clientTenant -> getClientTenant(clientId); consentInfo
 * -> getConsentInfo(clientId, scopes); clientBranding -> getClientBranding(clientId);
 * invitation -> verifyInvitation(token); verifyEmail
 * -> verifyEmail({ email, token }) (ONE object arg, unlike the 2-positional
 * `queryKeys.auth.verifyEmail(email, token)`); redirectValidation ->
 * validateRedirectUri(url, clientId).
 */
import { queryOptions } from "@tanstack/react-query";

import { createAuthClient, type AuthClient } from "../auth-client";
import { ensureQueryBootstrapped } from "./bootstrap";
import { queryKeys } from "./keys";

/**
 * The lazily-instantiated auth client. Built once on first access, AFTER
 * `ensureQueryBootstrapped()` has configured the shared client.
 */
let authClient: AuthClient | undefined;

/** Ensure the client is configured, then return the memoized shared auth client. */
function getAuthClient(): AuthClient {
  ensureQueryBootstrapped();
  authClient ??= createAuthClient();
  return authClient;
}

/**
 * queryOptions factories for wallow-auth's read-only flow lookups, each keyed off
 * `queryKeys.auth.*`.
 */
export const authQueries = {
  externalProviders: () =>
    queryOptions({
      queryKey: queryKeys.auth.externalProviders(),
      queryFn: () => getAuthClient().getExternalProviders(),
    }),
  clientTenant: (clientId: string) =>
    queryOptions({
      queryKey: queryKeys.auth.clientTenant(clientId),
      queryFn: () => getAuthClient().getClientTenant(clientId),
    }),
  consentInfo: (clientId: string, scopes?: readonly string[]) =>
    queryOptions({
      queryKey: queryKeys.auth.consentInfo(clientId, scopes),
      queryFn: () => getAuthClient().getConsentInfo(clientId, scopes),
    }),
  clientBranding: (clientId: string) =>
    queryOptions({
      queryKey: queryKeys.auth.clientBranding(clientId),
      queryFn: () => getAuthClient().getClientBranding(clientId),
    }),
  invitation: (token: string) =>
    queryOptions({
      queryKey: queryKeys.auth.invitation(token),
      queryFn: () => getAuthClient().verifyInvitation(token),
    }),
  verifyEmail: (email: string, token: string) =>
    queryOptions({
      queryKey: queryKeys.auth.verifyEmail(email, token),
      queryFn: () => getAuthClient().verifyEmail({ email, token }),
    }),
  redirectValidation: (url: string, clientId?: string) =>
    queryOptions({
      queryKey: queryKeys.auth.redirectValidation(url, clientId),
      queryFn: () => getAuthClient().validateRedirectUri(url, clientId),
    }),
};
