import { describe, expect, it } from "vitest";

/**
 * Register feature `api.ts` — a thin re-export seam over the SDK query entry.
 * `accountGetExternalProvidersOptions` is in login's seam too: each seam states
 * what THAT feature reaches, so the overlap is not duplication to collapse.
 *
 * Identity (`toBe`), not presence: a hand-written `accountRegisterMutation`
 * carries the same name, call shape and type, so a screen driving it passes
 * every behavioural spec while posting a body the OpenAPI doc does not describe.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, sorted. */
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
