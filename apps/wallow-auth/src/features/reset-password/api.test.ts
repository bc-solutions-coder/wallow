import { describe, expect, it } from "vitest";

/**
 * The reset-password `api.ts` seam, and why it re-exports a RAW SDK operation
 * where every other seam re-exports a generated `{op}Mutation()`: this screen
 * branches on the response status itself, because an expired or already-redeemed
 * token is a different screen, not a message beside a field.
 *
 * `accountResetPasswordMutation` is generated anyway; the pin is non-adoption.
 */

import * as sdk from "@bc-solutions-coder/sdk";
import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The raw POST this feature really reaches, on the barrel and behind the seam. */
const RAW_OPERATION = "accountResetPassword";

/** The seam's whole surface. */
const SURFACE: readonly string[] = [RAW_OPERATION];

/** The generated factory the SDK does emit for that POST, and this feature must never adopt. */
const GENERATED_FACTORY = "accountResetPasswordMutation";

describe("api.ts re-exports the raw reset-password operation", () => {
  it("re-exports it by identity from @bc-solutions-coder/sdk", () => {
    // Identity, not presence: a hand-written look-alike carries the same name,
    // call shape and type, passes the screen's specs, and spends a single-use
    // reset token against an endpoint the OpenAPI document does not describe.
    expect(api.accountResetPassword).toBe(sdk.accountResetPassword);
  });

  it("exposes nothing beyond the operation the reset-password feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});

describe("why this seam differs from every other one", () => {
  it("never adopts the generated mutation factory the SDK does emit for this POST", () => {
    // The first assertion is the PREMISE, stated so it cannot silently invert:
    // the factory is generated for every non-GET operation regardless of who
    // calls it, so it is one import away at all times. The second is the
    // invariant — the seam does not carry it. The screen behind the seam is held
    // by lint rather than here: this app's nested `no-restricted-imports` bans
    // `@bc-solutions-coder/sdk/query` under `src/features/**`, and only the seam
    // and this spec are exempt.
    expect(typeof query[GENERATED_FACTORY], `${GENERATED_FACTORY} is no longer generated`).toBe(
      "function",
    );
    expect(Object.keys(api), `the seam re-exports ${GENERATED_FACTORY}`).not.toContain(
      GENERATED_FACTORY,
    );
  });

  it("has no bare operation on the generated entry to prefer instead", () => {
    // The query entry carries factories only, never raw operations, so taking
    // `accountResetPassword` off the barrel is not someone missing a same-named
    // export one entry over.
    expect(RAW_OPERATION in query).toBe(false);
  });
});
