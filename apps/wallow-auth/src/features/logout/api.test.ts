import { describe, expect, it } from "vitest";

/**
 * Logout feature `api.ts` — a thin re-export seam over the SDK query entry,
 * which is where `LogoutScreen` reaches the API.
 *
 * Identity, not presence: a hand-written look-alike carries the same name, call
 * shape and type, and the screen driving it passes every behavioural spec while
 * asking a different endpoint whether a redirect URI is allow-listed — the one
 * question on this screen with a security answer. `toBe` rules that out.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, sorted. */
const SURFACE: readonly string[] = ["accountValidateRedirectUriOptions"];

describe("api.ts re-exports the SDK logout query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.accountValidateRedirectUriOptions).toBe(query.accountValidateRedirectUriOptions);
  });

  it("exposes nothing beyond the artifact the logout feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});
