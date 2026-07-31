import { describe, expect, it } from "vitest";

/**
 * Invitation feature `api.ts` — a thin re-export seam over the SDK query entry,
 * which is where `InvitationScreen` reaches the API.
 *
 * Identity, not presence: a hand-written `invitationsAcceptMutation` carries the
 * same name, call shape and type, and the screen driving it passes every
 * behavioural spec while posting a body the OpenAPI document does not describe.
 * `toBe` is the only assertion that rules that out.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, sorted. */
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
