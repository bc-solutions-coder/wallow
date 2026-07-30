import { describe, expect, it } from "vitest";

/**
 * Invitation feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4. `InvitationScreen`
 * imports from `../api`; both artifacts behind it are GENERATED from
 * `packages/sdk/openapi/v1.json`.
 *
 * Why identity and not just presence: a hand-written `invitationsAcceptMutation`
 * would carry the same name, the same call shape and the same type, and the
 * screen driving it would pass every behavioural spec while posting a body the
 * OpenAPI document does not describe. `toBe` is the only assertion that rules
 * that out.
 *
 * The `InvitationResponse` DTO stays on the raw barrel: a type is not a data
 * import, and re-exporting it here would put the seam in the business of
 * describing shapes rather than listing endpoints. What DOES belong here is the
 * pair — verify then accept — because reading the invitation and redeeming it are
 * the whole of what this feature reaches.
 *
 * Node project — it imports built package output and mounts nothing.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, in the order an ESM namespace enumerates it. */
const SURFACE: readonly string[] = ["invitationsAcceptMutation", "invitationsVerifyOptions"];

describe("api.ts re-exports the SDK invitation query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.invitationsVerifyOptions).toBe(query.invitationsVerifyOptions);
    expect(api.invitationsAcceptMutation).toBe(query.invitationsAcceptMutation);
  });

  it("exposes nothing beyond the artifacts the invitation feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});
