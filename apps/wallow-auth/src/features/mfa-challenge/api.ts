/**
 * MFA-challenge feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4.
 * `MfaChallengeForm` imports from `../api`; both artifacts behind it are GENERATED
 * from `packages/sdk/openapi/v1.json` and take `{ client }` — read the
 * request-scoped instance off the router context
 * (`useRouteContext({ from: "__root__" }).sdk`).
 *
 * An MFA code is lockout-counted and single-use, so the verify runs exactly once:
 * TanStack Query never retries a mutation by default, which is why the screen
 * carries no local `retry` override.
 *
 * `accountValidateRedirectUriOptions` appears here AND in `features/logout/api.ts`,
 * because both screens must confirm a return URL is allow-listed before handing
 * the browser to it. Each feature's seam states what THAT feature reaches; the
 * repetition is the point, not duplication to factor out into a shared module.
 *
 * `validateRedirectUriArgs`, `allowListedReturnUrl`, `buildExchangeTicketUrl` and
 * `isSafeReturnUrl` stay direct imports from the raw barrel: they build arguments
 * and URLs and test strings, and none of them issues a request.
 */
export {
  accountValidateRedirectUriOptions,
  accountVerifyMfaChallengeMutation,
} from "@bc-solutions-coder/sdk/query";
