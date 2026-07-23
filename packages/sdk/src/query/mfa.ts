/**
 * MFA query module — the Settings-page MFA status card + enroll flow's data
 * source.
 *
 * Ported from apps/wallow-web/src/features/mfa/api.ts. Rather than re-hand-rolling
 * the op-selection/request-body boilerplate, this module delegates to the SDK's
 * shared {@link MfaClient} (`createMfaClient(unwrap)`, per wallow-sdk.ts:287),
 * built LAZILY on first use so nothing touches the shared `@hey-api` client until
 * a query/mutation actually fires (see {@link getMfaClient}).
 *
 * INVALIDATION MODEL: the status card (`queryKeys.mfa.status()`) reflects
 * enabled/method/backupCodeCount. Confirming enrollment, disabling, and
 * regenerating backup codes all change that card, so each invalidates
 * `queryKeys.mfa.status()` on success. `enrollTotp` only mints a one-time
 * secret + QR (status stays disabled until confirm), so it does NOT invalidate —
 * its result is a one-time reveal held in component state, never cached.
 */
import { queryOptions, type QueryClient } from "@tanstack/react-query";

import { unwrap } from "../facade";
import { createMfaClient, type MfaClient } from "../mfa-client";
import { ensureQueryBootstrapped } from "./bootstrap";
import { queryKeys } from "./keys";

/**
 * The lazily-instantiated shared MFA client. Built once on first access, AFTER
 * `ensureQueryBootstrapped()` has configured the shared client — so it is never
 * created at module-load time (which would run before the app registers its
 * bootstrap configurator).
 */
let mfaClient: MfaClient | undefined;

/** Ensure the client is configured, then return the memoized shared MFA client. */
function getMfaClient(): MfaClient {
  ensureQueryBootstrapped();
  mfaClient ??= createMfaClient(unwrap);
  return mfaClient;
}

/**
 * queryOptions factory for MFA status, keyed `queryKeys.mfa.status()` so a single
 * `invalidateQueries({ queryKey: queryKeys.mfa.status() })` refreshes the card
 * after any state-changing mutation.
 */
export const mfaQueries = {
  status: () =>
    queryOptions({
      queryKey: queryKeys.mfa.status(),
      queryFn: () => getMfaClient().status(),
    }),
};

/** The confirm-enrollment variables (mirrors the API `MfaConfirmRequest`). */
export interface ConfirmEnrollBody {
  secret: string;
  code: string;
}

/**
 * Mutation factory: begin TOTP enrollment. No cache invalidation — the returned
 * `{ secret, qrUri }` is a one-time reveal held in component state; status only
 * changes once enrollment is confirmed.
 */
export const enrollTotpMutation = () => ({
  mutationFn: (): Promise<unknown> => getMfaClient().enrollTotp(),
});

/** Mutation factory: confirm enrollment; invalidates `queryKeys.mfa.status()` on success. */
export const confirmEnrollMutation = (queryClient: QueryClient) => ({
  mutationFn: (body: ConfirmEnrollBody): Promise<unknown> =>
    getMfaClient().confirmEnroll(body.secret, body.code),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.mfa.status() });
  },
});

/** Mutation factory: disable MFA (requires password); invalidates `queryKeys.mfa.status()` on success. */
export const disableMfaMutation = (queryClient: QueryClient) => ({
  mutationFn: (password: string): Promise<unknown> => getMfaClient().disable(password),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.mfa.status() });
  },
});

/** Mutation factory: regenerate backup codes (requires password); invalidates `queryKeys.mfa.status()` on success. */
export const regenerateBackupCodesMutation = (queryClient: QueryClient) => ({
  mutationFn: (password: string): Promise<unknown> =>
    getMfaClient().regenerateBackupCodes(password),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.mfa.status() });
  },
});
