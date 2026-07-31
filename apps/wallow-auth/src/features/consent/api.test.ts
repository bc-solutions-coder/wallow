import { describe, expect, it } from "vitest";

/**
 * Consent feature `api.ts` — a thin re-export seam over the SDK query entry.
 * `consentInfoArgs` and `buildConsentSubmitUrl` issue no request, so they stay
 * outside the seam and are imported straight from the barrel.
 *
 * Identity (`toBe`), not presence: a hand-written look-alike carries the same
 * name, call shape and type, so a screen driving it passes every behavioural
 * spec while requesting something the OpenAPI document does not describe.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, sorted. */
const SURFACE: readonly string[] = ["appsGetConsentInfoOptions"];

describe("api.ts re-exports the SDK consent query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.appsGetConsentInfoOptions).toBe(query.appsGetConsentInfoOptions);
  });

  it("exposes nothing beyond the artifact the consent feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});
