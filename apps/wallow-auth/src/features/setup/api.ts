/**
 * PROTOTYPE — wayfinder ticket #106 ("Prototype the setup page from existing
 * auth components"). Throwaway code on the `prototype/setup-page` branch; the
 * real implementation re-creates this seam properly.
 *
 * Setup feature api seam: the first-run status probe and the bootstrap-admin
 * create, both generated from `packages/sdk/openapi/v1.json`.
 */
export { setupCreateAdminMutation, setupGetStatusOptions } from "@bc-solutions-coder/sdk/query";
