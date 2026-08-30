/**
 * Consent feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4 to bring wallow-auth
 * in line with wallow-web: a feature's data surface is stated in ONE module, and
 * `ConsentScreen` imports from `../api` rather than naming the entry itself.
 *
 * The artifact below is GENERATED from `packages/sdk/openapi/v1.json`. Its factory
 * takes `{ client }` — read the request-scoped instance off the router context
 * (`useRouteContext({ from: "__root__" }).sdk`) and pass `sdk.client`; there is
 * nothing to configure or bootstrap first.
 *
 * ONE endpoint, because the consent screen only READS: the grant itself is a
 * full-page form POST to the OIDC endpoint, not an SDK call. `consentInfoArgs`
 * and `buildConsentSubmission` stay direct imports from the raw barrel at the
 * call site — they build arguments and a form, and neither issues a request.
 * Pulling them behind the seam would turn this one-line endpoint list into a
 * second barrel.
 */
export { appsGetConsentInfoOptions } from "@bc-solutions-coder/sdk/query";
