/**
 * MFA feature view types (Wallow-8w1h.6.3) — the narrowing boundary for the
 * untyped-response gap. The generated MFA ops resolve `unknown` bodies (the
 * backend declares no `ProducesResponseType`), so both reference apps hand-wrote
 * the same response interfaces and narrowed at their render boundary.
 *
 * As of Wallow-0q2s.9.3 those five interfaces are centralized in the SDK's
 * shared MFA slice (`@bc-solutions-coder/sdk`'s `mfa-client.ts`), and the facade
 * `mfa` slice (`src/lib/wallow-sdk.ts`) — built by `createMfaClient(unwrap)` —
 * already returns them typed. This module re-exports them so the feature's
 * components keep importing their view types from `../types` unchanged.
 *
 * Oracles (unchanged):
 *  - `MfaStatusResponse`  -> api/src/Wallow.Web/Models/MfaStatusResponse.cs
 *                            (`MfaController.Status` `Ok(new { enabled, method, backupCodeCount })`)
 *  - `MfaEnrollResponse`  -> `MfaController.EnrollTotp` `Ok(new { secret, qrUri })`
 *  - `MfaConfirmResponse` -> `MfaController.ConfirmEnrollment` `Ok(new { succeeded, backupCodes })`
 *                            with `{ succeeded:false, error }` on failure
 *  - `MfaDisableResponse` -> `MfaController.Disable` `Ok(new { succeeded })`
 *                            with `{ succeeded:false, error }` on failure
 *  - `MfaRegenerateBackupCodesResponse` -> `MfaController.RegenerateBackupCodes`
 *                            `Ok(new { codes })`
 */

export type {
  MfaConfirmResponse,
  MfaDisableResponse,
  MfaEnrollResponse,
  MfaRegenerateBackupCodesResponse,
  MfaStatusResponse,
} from "@bc-solutions-coder/sdk";
