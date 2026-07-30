import { describe, expect, it } from "vitest";

/**
 * Register feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4. `RegisterForm`
 * imports from `../api`; everything behind it is GENERATED from
 * `packages/sdk/openapi/v1.json`.
 *
 * Why identity and not just presence: a hand-written `accountRegisterMutation`
 * closing over its own `fetch` would carry the same name, the same call shape and
 * the same type, and the screen driving it would pass every behavioural spec
 * while posting a body the OpenAPI document does not describe. `toBe` is the only
 * assertion that rules that out.
 *
 * `accountGetExternalProvidersOptions` appears in this seam AND in login's. That
 * is not duplication to remove: both screens offer the same external-provider
 * buttons, and each feature's seam is a statement of what THAT feature reaches.
 * Collapsing them into a shared module would make the sign-up surface unreadable
 * from the sign-up feature.
 *
 * Node project — it imports built package output and mounts nothing.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, in the order an ESM namespace enumerates it. */
const SURFACE: readonly string[] = [
  "accountGetClientTenantOptions",
  "accountGetExternalProvidersOptions",
  "accountRegisterMutation",
];

describe("api.ts re-exports the SDK register query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.accountRegisterMutation).toBe(query.accountRegisterMutation);
    expect(api.accountGetClientTenantOptions).toBe(query.accountGetClientTenantOptions);
    expect(api.accountGetExternalProvidersOptions).toBe(query.accountGetExternalProvidersOptions);
  });

  it("exposes nothing beyond the artifacts the register feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});
