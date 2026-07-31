import { describe, expect, it } from "vitest";

/**
 * Verify-email `api.ts` — a thin re-export seam over the SDK query entry, whose
 * artifact is generated from `packages/sdk/openapi/v1.json`.
 *
 * Identity (`toBe`), not presence: the confirmation token is single-use, so a
 * hand-written look-alike would burn it against the wrong endpoint and leave the
 * user with a link that cannot be retried. `isSafeReturnUrl` stays on the raw
 * barrel — it is a synchronous predicate over a string, not a request.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

const SURFACE: readonly string[] = ["accountVerifyEmailOptions"];

describe("api.ts re-exports the SDK verify-email query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.accountVerifyEmailOptions).toBe(query.accountVerifyEmailOptions);
  });

  it("exposes nothing beyond the artifact the verify-email feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});
