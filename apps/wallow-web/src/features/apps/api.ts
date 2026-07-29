/**
 * Apps feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`). Routes/components keep importing from
 * `./api`; as of Wallow-pu6a.5.5 everything behind it is GENERATED from the
 * OpenAPI document, so each factory takes an explicit `{ client }` (the
 * request-scoped instance off the router context) and there is no hand-written
 * `appsQueries` namespace left to configure.
 */
export {
  appsGetUserAppsOptions,
  appsGetUserAppsQueryKey,
  appsRegisterMutation,
  clientBrandingUpsertBrandingMutation,
  queriesForOperation,
  queriesWithTag,
} from "@bc-solutions-coder/sdk/query";
