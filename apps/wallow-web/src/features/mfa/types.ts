/**
 * MFA feature view types (Wallow-8w1h.6.3) — the narrowing boundary for the
 * untyped-response gap. The generated MFA ops resolve `unknown` bodies (the
 * backend declares no `ProducesResponseType`), so the facade `mfa` slice returns
 * `Promise<unknown>`; these interfaces (mirroring the C# response records/shapes
 * 1:1) are what the query/mutation results are cast to at the render boundary,
 * instead of leaking `any`.
 *
 * Oracles:
 *  - `MfaStatusResponse`  -> api/src/Wallow.Web/Models/MfaStatusResponse.cs
 *                            (also `MfaController.Status` `Ok(new { enabled, method, backupCodeCount })`)
 *  - `MfaEnrollResponse`  -> api/src/Wallow.Auth/Services/IAuthApiClient.cs:38
 *                            (`MfaController.EnrollTotp` `Ok(new { secret, qrUri })`)
 *  - `MfaConfirmResponse` -> `MfaConfirmEnrollmentResponse` (IAuthApiClient.cs:39);
 *                            `MfaController.ConfirmEnrollment` `Ok(new { succeeded, backupCodes })`
 *                            with `{ succeeded:false, error }` on failure
 *  - `MfaDisableResponse` -> `MfaController.Disable` `Ok(new { succeeded })`
 *                            with `{ succeeded:false, error }` on failure
 *  - `MfaRegenerateBackupCodesResponse` -> `MfaController.RegenerateBackupCodes`
 *                            `Ok(new { codes })`
 */

/** MFA status card model (`GET /v1/identity/mfa/status`). */
export interface MfaStatusResponse {
  enabled: boolean;
  method: string | null;
  backupCodeCount: number;
}

/** TOTP enrollment payload (`POST /v1/identity/mfa/enroll/totp`) — one-time secret + QR. */
export interface MfaEnrollResponse {
  secret: string;
  qrUri: string;
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
