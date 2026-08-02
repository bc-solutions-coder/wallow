import { describe, expect, it } from "vitest";

/**
 * Forgot-password `api.ts` — a thin re-export seam over the RAW SDK barrel, not
 * the generated `{op}Mutation()` factory every other seam re-exports: the screen
 * must report the same outcome whether or not the address exists, and a mutation's
 * error surface would make the form an account-enumeration oracle.
 *
 * Identity (`toBe`), not presence — a hand-written look-alike carries the same
 * name, shape and type, passes every behavioural spec, and reaches an endpoint
 * the OpenAPI document does not describe.
 */

import * as sdk from "@bc-solutions-coder/sdk";
import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

const RAW_OPERATION = "accountForgotPassword";

const SURFACE: readonly string[] = [RAW_OPERATION];

/** The factory the SDK does emit for that POST, and this feature must never adopt. */
const GENERATED_FACTORY = "accountForgotPasswordMutation";

describe("api.ts re-exports the raw forgot-password operation", () => {
  it("re-exports it by identity from @bc-solutions-coder/sdk", () => {
    expect(api.accountForgotPassword).toBe(sdk.accountForgotPassword);
  });

  it("exposes nothing beyond the operation the forgot-password feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});

describe("why this seam differs from every other one", () => {
  it("never adopts the generated mutation factory the SDK does emit for this POST", () => {
    // The first assertion is the PREMISE, stated so it cannot silently invert: the
    // generator emits an `{op}Mutation()` for every non-GET operation regardless of
    // who calls it, so this factory exists and is one import away at all times. The
    // second is the invariant — the seam does not carry it. The screen behind the
    // seam is held by lint rather than here: this app's nested `no-restricted-imports`
    // bans `@bc-solutions-coder/sdk/query` under `src/features/**`, and only the seam
    // and this spec are exempt, so no component can reach the factory at all.
    expect(typeof query[GENERATED_FACTORY], `${GENERATED_FACTORY} is no longer generated`).toBe(
      "function",
    );
    expect(Object.keys(api), `the seam re-exports ${GENERATED_FACTORY}`).not.toContain(
      GENERATED_FACTORY,
    );
  });

  it("has no bare operation on the generated entry to prefer instead", () => {
    // The query entry carries factories only, never the raw operation, so
    // re-exporting `accountForgotPassword` from the barrel is not someone missing a
    // same-named export one entry over.
    expect(RAW_OPERATION in query).toBe(false);
  });
});
