/**
 * Invitation feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4. `InvitationScreen`
 * imports from `../api`; both artifacts behind it are GENERATED from
 * `packages/sdk/openapi/v1.json` and take `{ client }` — read the request-scoped
 * instance off the router context (`useRouteContext({ from: "__root__" }).sdk`).
 *
 * The pair IS the feature: read the invitation, then redeem it. Nothing else on
 * this screen reaches the API.
 *
 * Two things stay outside the seam on purpose. The `InvitationResponse` DTO is
 * imported straight from the raw barrel at the call site — a type is not a data
 * import, and re-exporting it here would put the seam in the business of
 * describing shapes rather than listing endpoints. And the accept mutation's
 * success arm (a full-page navigation) is declared at the `useMutation` call site:
 * the generated factory bakes in NO `onSuccess`, so the screen SPREADS the factory
 * into its options rather than passing it straight through.
 */
export { invitationsAcceptMutation, invitationsVerifyOptions } from "@bc-solutions-coder/sdk/query";
