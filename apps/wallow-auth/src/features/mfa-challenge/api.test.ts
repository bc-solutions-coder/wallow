import { describe, expect, it } from "vitest";

/**
 * MFA-challenge feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4.
 * `MfaChallengeForm` imports from `../api`; both artifacts behind it are
 * GENERATED from `packages/sdk/openapi/v1.json`.
 *
 * Why identity and not just presence, and why it matters more here than in most
 * seams: an MFA code is lockout-counted and single-use. A hand-written
 * `accountVerifyMfaChallengeMutation` would carry the same name, the same call
 * shape and the same type, pass every behavioural spec, and spend a user's
 * attempt against something other than the documented endpoint. `toBe` is the
 * only assertion that rules that out.
 *
 * `accountValidateRedirectUriOptions` appears in this seam AND in logout's,
 * because both screens must confirm a return URL is allow-listed before they hand
 * the browser to it. Each feature's seam states what THAT feature reaches; the
 * repetition is the point, not duplication to factor out.
 *
 * Node project — it imports built package output and mounts nothing.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, in the order an ESM namespace enumerates it. */
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
