import { describe, expect, it } from "vitest";

/**
 * MFA-enroll feature `api.ts` — the re-export seam its screen imports from, and the one seam
 * here spanning both SDK entries.
 *
 * The token exchange stays a RAW operation because sequencing is load-bearing: it mints the
 * `Identity.MfaPartial` cookie `enroll/totp` authenticates with, so the screen awaits it
 * imperatively before starting enrollment, and a `useMutation` would put that ordering
 * behind a callback.
 *
 * Node project — it imports built package output and mounts nothing.
 */

import * as sdk from "@bc-solutions-coder/sdk";
import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The imperative POST behind the seam: raw, and awaited before enrollment starts. */
const RAW_OPERATION = "mfaExchangeEnrollmentToken";

const SURFACE: readonly string[] = [
  "mfaConfirmEnrollmentMutation",
  "mfaEnrollTotpMutation",
  RAW_OPERATION,
];

/** The generated factory the SDK does emit for that POST, and this feature must never adopt. */
const GENERATED_FACTORY = "mfaExchangeEnrollmentTokenMutation";

describe("api.ts re-exports the SDK mfa-enroll query surface", () => {
  it("re-exports each generated symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.mfaEnrollTotpMutation).toBe(query.mfaEnrollTotpMutation);
    expect(api.mfaConfirmEnrollmentMutation).toBe(query.mfaConfirmEnrollmentMutation);
  });

  it("exposes nothing beyond the artifacts the mfa-enroll feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});

describe("the imperative enrollment-token exchange stays a raw operation", () => {
  it("re-exports it by identity from the raw @bc-solutions-coder/sdk barrel", () => {
    expect(api.mfaExchangeEnrollmentToken).toBe(sdk.mfaExchangeEnrollmentToken);
  });

  it("never adopts the generated mutation factory the SDK does emit for this POST", () => {
    // The generator emits an `{op}Mutation()` for every non-GET operation regardless of who
    // calls it, so one is waiting for this POST one import away from the two factories the
    // seam does re-export. The first assertion states that premise so it cannot silently
    // invert; the second holds the invariant that the seam does not carry it. The screen
    // behind the seam is held by lint rather than here: this app's nested
    // `no-restricted-imports` bans `@bc-solutions-coder/sdk/query` under `src/features/**`
    // with only the seam and this spec exempt.
    expect(typeof query[GENERATED_FACTORY], `${GENERATED_FACTORY} is no longer generated`).toBe(
      "function",
    );
    expect(Object.keys(api), `the seam re-exports ${GENERATED_FACTORY}`).not.toContain(
      GENERATED_FACTORY,
    );
  });

  it("has no bare operation on the generated entry to prefer instead", () => {
    // The query entry carries factories only, never the raw operation, so taking the
    // exchange off the barrel is not someone missing a same-named export one entry over.
    expect(RAW_OPERATION in query).toBe(false);
  });
});
