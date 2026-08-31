import { describe, expect, it } from "vitest";

/**
 * Consent feature `api.ts` — a thin re-export seam over the SDK query entry.
 * The one read is the transaction-scoped authorize-context lookup, shared with
 * the root loader's cache. `buildConsentSubmission` issues no request, so it
 * stays outside the seam and is imported straight from the barrel.
 *
 * Identity (`toBe`), not presence: a hand-written look-alike carries the same
 * name, call shape and type, so a screen driving it passes every behavioural
 * spec while requesting something the OpenAPI document does not describe.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, sorted. */
const SURFACE: readonly string[] = ["authorizeContextGetOptions"];

describe("api.ts re-exports the SDK consent query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.authorizeContextGetOptions).toBe(query.authorizeContextGetOptions);
  });

  it("exposes nothing beyond the artifact the consent feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});
