import { describe, expect, it } from "vitest";

/**
 * Consent feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4. `ConsentScreen`
 * imports from `../api`; the artifact behind it is GENERATED from
 * `packages/sdk/openapi/v1.json`.
 *
 * Why identity and not just presence: a hand-written look-alike would carry the
 * same name, the same call shape and the same type, and the screen driving it
 * would pass every behavioural spec while requesting something the OpenAPI
 * document does not describe. `toBe` is the only assertion that rules that out.
 *
 * The consent screen reaches ONE endpoint through this seam and two pure helpers
 * — `consentInfoArgs` and `buildConsentSubmitUrl` — straight from the raw barrel.
 * That asymmetry is deliberate: neither helper issues a request (the submit is a
 * full-page form POST to the OIDC endpoint, not an SDK call), so pulling them
 * behind the seam would turn a one-line endpoint list into a second barrel.
 *
 * Node project — it imports built package output and mounts nothing.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, in the order an ESM namespace enumerates it. */
const SURFACE: readonly string[] = ["appsGetConsentInfoOptions"];

describe("api.ts re-exports the SDK consent query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.appsGetConsentInfoOptions).toBe(query.appsGetConsentInfoOptions);
  });

  it("exposes nothing beyond the artifact the consent feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});
