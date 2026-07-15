/**
 * MFA feature query layer (Wallow-8w1h.6.3) — copies the CANONICAL Organizations
 * `api.ts` shape: a `queryOptions` factory over `getWallowSdk().mfa` plus mutation
 * factories that close over the router `QueryClient`.
 *
 * INVALIDATION MODEL: the MFA status card (`['mfa', 'status']`) reflects
 * enabled/method/backupCodeCount. Confirming enrollment, disabling, and
 * regenerating backup codes all change that card, so each invalidates
 * `['mfa', 'status']` on success. `enrollTotp` only mints a one-time secret+QR
 * (status stays disabled until confirm), so it does NOT invalidate — its result
 * is held in component state (one-time reveal), like the apps client-secret flow.
 *
 * UNTYPED-RESPONSE GAP: the facade slice returns `Promise<unknown>`; components
 * cast results to the `src/features/mfa/types.ts` interfaces at the render
 * boundary.
 */
import { queryOptions, type QueryClient } from "@tanstack/react-query";

import { getWallowSdk } from "../../lib/wallow-sdk";

/**
 * queryOptions factory for MFA status, keyed `['mfa', 'status']` so a single
 * `invalidateQueries({ queryKey: ['mfa', 'status'] })` refreshes the card after
 * any state-changing mutation.
 */
export const mfaQueries = {
  status: () =>
    queryOptions({
      queryKey: ["mfa", "status"] as const,
      queryFn: (): Promise<unknown> => getWallowSdk().mfa.status(),
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
  mutationFn: (): Promise<unknown> => getWallowSdk().mfa.enrollTotp(),
});

/** Mutation factory: confirm enrollment; invalidates `['mfa', 'status']` on success. */
export const confirmEnrollMutation = (queryClient: QueryClient) => ({
  mutationFn: (body: ConfirmEnrollBody): Promise<unknown> =>
    getWallowSdk().mfa.confirmEnroll(body.secret, body.code),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["mfa", "status"] });
  },
});

/** Mutation factory: disable MFA (requires password); invalidates `['mfa', 'status']` on success. */
export const disableMfaMutation = (queryClient: QueryClient) => ({
  mutationFn: (password: string): Promise<unknown> => getWallowSdk().mfa.disable(password),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["mfa", "status"] });
  },
});

/** Mutation factory: regenerate backup codes (requires password); invalidates `['mfa', 'status']` on success. */
export const regenerateBackupCodesMutation = (queryClient: QueryClient) => ({
  mutationFn: (password: string): Promise<unknown> =>
    getWallowSdk().mfa.regenerateBackupCodes(password),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["mfa", "status"] });
  },
});
