/**
 * MFA-enroll feature `api.ts` — a THIN RE-EXPORT SEAM added by Wallow-x4qn.9.4,
 * and the ONE seam in this app that spans BOTH SDK entries. `MfaEnrollForm` imports
 * all three artifacts from `../api`.
 *
 * Two are GENERATED from `packages/sdk/openapi/v1.json` and take `{ client }` —
 * read the request-scoped instance off the router context
 * (`useRouteContext({ from: "__root__" }).sdk`): `mfaEnrollTotpMutation` mints the
 * shared secret the QR code renders, `mfaConfirmEnrollmentMutation` confirms the
 * first code.
 *
 * WHY THE THIRD COMES FROM THE RAW BARREL — read this before "fixing" it. The
 * reason is SEQUENCING, not shape. `mfaExchangeEnrollmentToken` is what mints the
 * `Identity.MfaPartial` cookie, and `enroll/totp` fired without it simply 401s, so
 * the screen awaits the exchange inside its mount effect's `try`/`catch` BEFORE
 * starting the enrollment — precisely so a failed exchange skips a call that could
 * only fail. A `useMutation` would put that ordering behind a callback.
 * Wallow-x4qn.9.3 left it imperative on purpose. The generator DOES emit
 * `mfaExchangeEnrollmentTokenMutation` — it emits one for every non-GET operation,
 * whether or not anything calls it — so a third factory really is sitting beside the
 * two above, and NOT ADOPTING IT IS THE POINT. `api.test.ts` beside this file pins
 * that: the factory exists on the query entry, and neither this seam nor the screen
 * behind it names it, so the raw import cannot be read as an oversight.
 *
 * It is still DATA, which is why it belongs here: this seam exists so `api.ts` is
 * the feature's only data import, not so it is the feature's only *generated*
 * import.
 *
 * The `MfaEnrollmentConfirmedResponse` DTO and `isSafeReturnUrl` stay direct
 * imports from the raw barrel at the call site: a type is not a data import, and a
 * return-url predicate issues no request.
 */
export { mfaExchangeEnrollmentToken } from "@bc-solutions-coder/sdk";
export { mfaConfirmEnrollmentMutation, mfaEnrollTotpMutation } from "@bc-solutions-coder/sdk/query";
