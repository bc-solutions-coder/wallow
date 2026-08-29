/**
 * Verify-email feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4.
 * `VerifyEmailConfirm` imports from `../api`; the artifact behind it is GENERATED
 * from `packages/sdk/openapi/v1.json` and takes `{ client }` — read the
 * request-scoped instance off the router context
 * (`useRouteContext({ from: "__root__" }).sdk`).
 *
 * A confirmation link is single-use, so the redemption is a read the query client
 * runs exactly once (`createQueryClient()` disables query retries globally); an
 * `{op}Options()` factory is what the generator emits for the `GET`.
 *
 * The open-redirect guard is not named here: this screen and the feature's
 * `sign-in-href.ts` read their verdicts from `@shared/lib/return-url`'s
 * `decideReturnUrl`, which issues no request — the seam lists this feature's
 * ENDPOINTS.
 */
export { accountVerifyEmailOptions } from "@bc-solutions-coder/sdk/query";
