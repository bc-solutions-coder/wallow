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
 * full-page form POST to the OIDC endpoint, not an SDK call. The read is the
 * TRANSACTION-scoped context lookup — keyed by the pending authorize request's
 * `returnUrl`, never by a bare `client_id`, so nothing about a client is
 * disclosed to a crafted link. It is the same query the root loader resolves
 * for the branded chrome, so by the time the screen asks, the answer is
 * normally already in the cache. `buildConsentSubmission` stays a direct import
 * from the raw barrel at the call site — it builds a form and issues no request.
 */
export { authorizeContextGetOptions } from "@bc-solutions-coder/sdk/query";
