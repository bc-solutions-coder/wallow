/**
 * Setup feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`). Everything behind it is GENERATED from
 * `packages/sdk/openapi/v1.json` and takes `{ client }` — read the
 * request-scoped instance off the router context.
 *
 * Two artifacts, which is the whole of what first-run setup reaches: ask
 * whether setup is still open (`GET /v1/identity/setup/status`, the one
 * endpoint that answers pre-setup without a 503) and create the bootstrap
 * administrator (`POST /v1/identity/setup/admin` — user, organization, and
 * owner membership in one command; a 409 means another session finished first).
 */
export { setupCreateAdminMutation, setupGetStatusOptions } from "@bc-solutions-coder/sdk/query";
