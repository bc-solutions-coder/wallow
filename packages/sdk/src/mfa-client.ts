/**
 * `createMfaClient(unwrap)` — the shared, unwrap-INJECTED MFA API-wrapper slice
 * (Wallow-0q2s.9.3).
 *
 * Both reference apps drive the SAME enroll/confirm/status/disable/backup-codes
 * TOTP endpoints, and both hand-rolled the same op-selection + request-body
 * boilerplate (wallow-web in `src/lib/wallow-sdk.ts`'s `mfa` slice, wallow-auth
 * in `createAuthClient()`'s inline MFA methods). This module extracts exactly
 * that shared part into the SDK.
 *
 * WHY `unwrap` IS INJECTED (the crux of the 9.3 decision): the two apps'
 * envelope-unwrapping ERROR semantics deliberately diverge and MUST stay
 * divergent —
 *
 *   - wallow-auth uses `createAuthClient()`'s private `unwrap`, which throws a
 *     typed {@link WallowError} parsed from the RFC 7807 problem details;
 *     `MfaEnrollForm.tsx` reads `error.code` / `error.status` off that shape.
 *   - wallow-web uses `facade.ts`'s exported `unwrap`, which throws the RAW
 *     unparsed error object; `features/mfa/errors.ts` (`problemDetail`) reads
 *     `(error as ProblemDetails).detail` and `(error as { error }).error`
 *     straight off the raw body.
 *
 * Merging the wrapper functions onto ONE `unwrap` would rewrite one app's
 * error-mapping code — which 9.3's DESIGN forbids ("keep view/flow logic
 * per-app, untouched"). Parameterizing `unwrap` dissolves the conflict: the SDK
 * owns which generated op each method calls, the request-body shape, and the
 * response TYPE; each app injects its own `unwrap`, so error policy — and thus
 * runtime behavior — stays exactly what it is today per app.
 *
 * TYPED RESPONSES: every MFA op resolves a `200: unknown` body in the OpenAPI
 * spec (the controllers carry no `ProducesResponseType`), so both apps
 * hand-wrote the same response interfaces and narrowed `unknown` at their render
 * boundary. This module centralizes those interfaces (below) and performs the
 * narrowing ONCE, in the wrapper, so callers receive a typed result instead of
 * `unknown`. The request types (`MfaConfirmRequest` etc.) are already generated
 * and re-exported from the SDK root; apps import those directly.
 */

import {
  getV1IdentityMfaStatus,
  postV1IdentityMfaBackupCodesRegenerate,
  postV1IdentityMfaDisable,
  postV1IdentityMfaEnrollConfirm,
  postV1IdentityMfaEnrollTotp,
} from "./generated";
import type { SdkEnvelope } from "./facade";

/**
 * The envelope-unwrapping strategy a consuming app injects. Return `data` on
 * success; THROW on failure with whatever error shape that app's components
 * expect (raw `ProblemDetails` for wallow-web, a typed `WallowError` for
 * wallow-auth). Both apps' existing `unwrap` helpers already satisfy this shape.
 */
export type MfaUnwrap = <TData>(pending: Promise<SdkEnvelope<TData>>) => Promise<TData>;

/** MFA status card model (`GET /v1/identity/mfa/status`). */
export interface MfaStatusResponse {
  enabled: boolean;
  method: string | null;
  backupCodeCount: number;
}

/** TOTP enrollment payload (`POST /v1/identity/mfa/enroll/totp`) — one-time secret + QR. */
export interface MfaEnrollResponse {
  secret: string;
  qrUri: string | null;
}

/**
 * Enrollment-confirmation result (`POST /v1/identity/mfa/enroll/confirm`).
 * On success `succeeded` is true and `backupCodes` carries the one-time codes;
 * on failure `succeeded` is false and `error` carries the machine code.
 */
export interface MfaConfirmResponse {
  succeeded: boolean;
  backupCodes?: string[];
  error?: string;
}

/** Disable result (`POST /v1/identity/mfa/disable`). */
export interface MfaDisableResponse {
  succeeded: boolean;
  error?: string;
}

/** Regenerated backup codes (`POST /v1/identity/mfa/backup-codes/regenerate`). */
export interface MfaRegenerateBackupCodesResponse {
  codes: string[];
}

/**
 * The shared MFA facade. One method per MFA-management endpoint, with typed
 * request args and typed responses. wallow-web consumes all five; wallow-auth
 * consumes `enrollTotp` + `confirmEnroll` (its mid-login challenge flow never
 * needs status/disable/regenerate).
 */
export interface MfaClient {
  /** Read MFA status (`GET /v1/identity/mfa/status`). */
  status: () => Promise<MfaStatusResponse>;
  /** Begin TOTP enrollment (no body); mints a one-time secret + QR. */
  enrollTotp: () => Promise<MfaEnrollResponse>;
  /** Confirm enrollment with the TOTP `secret` (from `enrollTotp`) + user `code`. */
  confirmEnroll: (secret: string, code: string) => Promise<MfaConfirmResponse>;
  /** Disable MFA (requires the account `password`). */
  disable: (password: string) => Promise<MfaDisableResponse>;
  /** Regenerate backup codes (requires the account `password`). */
  regenerateBackupCodes: (password: string) => Promise<MfaRegenerateBackupCodesResponse>;
}

/**
 * Build the shared MFA facade over the generated MFA ops, routing every
 * `{ data, error }` envelope through the injected {@link MfaUnwrap}.
 *
 * @param unwrap The consuming app's envelope-unwrapping strategy — the seam that
 *   keeps per-app error semantics intact. See the module header.
 */
export function createMfaClient(unwrap: MfaUnwrap): MfaClient {
  return {
    status: () => unwrap(getV1IdentityMfaStatus() as Promise<SdkEnvelope<MfaStatusResponse>>),
    enrollTotp: () =>
      unwrap(postV1IdentityMfaEnrollTotp() as Promise<SdkEnvelope<MfaEnrollResponse>>),
    confirmEnroll: (secret: string, code: string) =>
      unwrap(
        postV1IdentityMfaEnrollConfirm({ body: { secret, code } }) as Promise<
          SdkEnvelope<MfaConfirmResponse>
        >,
      ),
    disable: (password: string) =>
      unwrap(
        postV1IdentityMfaDisable({ body: { password } }) as Promise<
          SdkEnvelope<MfaDisableResponse>
        >,
      ),
    regenerateBackupCodes: (password: string) =>
      unwrap(
        postV1IdentityMfaBackupCodesRegenerate({ body: { password } }) as Promise<
          SdkEnvelope<MfaRegenerateBackupCodesResponse>
        >,
      ),
  };
}
