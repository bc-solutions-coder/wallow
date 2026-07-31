import { describe, expect, it } from "vitest";

/**
 * MFA-challenge feature `api.ts` — the re-export seam its screen imports from.
 *
 * Identity (`toBe`), not presence: an MFA code is lockout-counted and single-use, so a
 * hand-written look-alike would carry the same name, shape and type, pass every behavioural
 * spec, and spend a user's attempt against something other than the documented endpoint.
 *
 * Node project — it imports built package output and mounts nothing.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

const SURFACE: readonly string[] = [
  "accountValidateRedirectUriOptions",
  "accountVerifyMfaChallengeMutation",
];

describe("api.ts re-exports the SDK mfa-challenge query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.accountVerifyMfaChallengeMutation).toBe(query.accountVerifyMfaChallengeMutation);
    expect(api.accountValidateRedirectUriOptions).toBe(query.accountValidateRedirectUriOptions);
  });

  it("exposes nothing beyond the artifacts the mfa-challenge feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});
