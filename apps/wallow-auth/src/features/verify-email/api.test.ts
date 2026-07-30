import { describe, expect, it } from "vitest";

/**
 * Verify-email feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4.
 * `VerifyEmailConfirm` imports from `../api`; the artifact behind it is GENERATED
 * from `packages/sdk/openapi/v1.json`.
 *
 * Why identity and not just presence: the confirmation token is single-use, so a
 * hand-written look-alike — same name, same call shape, same type, passing every
 * behavioural spec — would burn it against the wrong endpoint and leave the user
 * with a link that cannot be retried. `toBe` is the only assertion that rules
 * that out.
 *
 * `isSafeReturnUrl` stays on the raw barrel here and in the feature's
 * `sign-in-href.ts`: it is a synchronous predicate over a string, not a request.
 *
 * Node project — it imports built package output and mounts nothing.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, in the order an ESM namespace enumerates it. */
const SURFACE: readonly string[] = ["accountVerifyEmailOptions"];

describe("api.ts re-exports the SDK verify-email query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.accountVerifyEmailOptions).toBe(query.accountVerifyEmailOptions);
  });

  it("exposes nothing beyond the artifact the verify-email feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});
