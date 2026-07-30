/**
 * Register feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4. `RegisterForm`
 * imports from `../api`; everything behind it is GENERATED from
 * `packages/sdk/openapi/v1.json` and takes `{ client }` — read the request-scoped
 * instance off the router context (`useRouteContext({ from: "__root__" }).sdk`).
 *
 * Three artifacts, which is the whole of what sign-up reaches: resolve the
 * client's tenant to paint the form, list the external providers to offer beside
 * it, and post the registration.
 *
 * `accountGetExternalProvidersOptions` appears here AND in `features/login/api.ts`.
 * That is not duplication to remove: both screens offer the same provider buttons,
 * and each feature's seam is a statement of what THAT feature reaches — collapsing
 * them into a shared module would make the sign-up surface unreadable from the
 * sign-up feature.
 *
 * `isSafeReturnUrl` stays a direct import from the raw barrel: it is a synchronous
 * predicate over a string, not a request.
 */
export {
  accountGetClientTenantOptions,
  accountGetExternalProvidersOptions,
  accountRegisterMutation,
} from "@bc-solutions-coder/sdk/query";
