/**
 * Logout feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4. `LogoutScreen`
 * imports from `../api`; the artifact behind it is GENERATED from
 * `packages/sdk/openapi/v1.json` and takes `{ client }` — read the request-scoped
 * instance off the router context (`useRouteContext({ from: "__root__" }).sdk`).
 *
 * ONE endpoint, and it is the security-bearing one: before this screen hands the
 * browser to a post-logout redirect it asks the API whether that URI is
 * allow-listed for the client. `validateRedirectUriArgs` (which builds this
 * operation's arguments) and `buildConnectLogoutUrl` (which builds the OIDC
 * end-session URL the browser is finally sent to) stay direct imports from the raw
 * barrel: neither issues a request. The seam lists the endpoint; the helpers shape
 * what is handed to it.
 */
export { accountValidateRedirectUriOptions } from "@bc-solutions-coder/sdk/query";
