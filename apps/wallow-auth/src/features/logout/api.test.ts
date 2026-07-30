import { describe, expect, it } from "vitest";

/**
 * Logout feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`), added by Wallow-x4qn.9.4. `LogoutScreen`
 * imports from `../api`; the artifact behind it is GENERATED from
 * `packages/sdk/openapi/v1.json`.
 *
 * Why identity and not just presence: a hand-written look-alike would carry the
 * same name, the same call shape and the same type, and the screen driving it
 * would pass every behavioural spec while asking a different endpoint whether a
 * redirect URI is allow-listed — the one question on this screen with a security
 * answer. `toBe` is the only assertion that rules that out.
 *
 * `validateRedirectUriArgs` and `buildConnectLogoutUrl` stay on the raw barrel:
 * the first builds this operation's arguments and the second builds a URL, and
 * neither issues a request. The seam lists the endpoint; the helpers shape what
 * is handed to it.
 *
 * Node project — it imports built package output and mounts nothing.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

/** The seam's whole surface, in the order an ESM namespace enumerates it. */
const SURFACE: readonly string[] = ["accountValidateRedirectUriOptions"];

describe("api.ts re-exports the SDK logout query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.accountValidateRedirectUriOptions).toBe(query.accountValidateRedirectUriOptions);
  });

  it("exposes nothing beyond the artifact the logout feature uses", () => {
    expect(Object.keys(api).toSorted()).toEqual(SURFACE);
  });
});
