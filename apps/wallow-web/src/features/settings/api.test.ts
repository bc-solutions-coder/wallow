import { describe, expect, it } from "vitest";

/**
 * Settings (Profile) feature `api.ts` — a THIN RE-EXPORT SEAM over
 * `@bc-solutions-coder/sdk/query`. Profile is READ-ONLY (no mutation endpoint),
 * so the seam exposes only the current-user read.
 *
 * The behavioural point Wallow-pu6a.5.5 introduced, and what this spec pins, is
 * that the profile screen and the dashboard's auth guard now read the SAME
 * generated operation. Under the hand-written layer they had separate keys
 * (`['settings','profile']` vs `['user','current']`) and therefore two cache
 * entries and two requests for one resource; the generated key makes them one,
 * and that must not silently regress into a bespoke wrapper key again.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import * as api from "./api";

describe("api.ts re-exports the SDK settings query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.usersGetCurrentUserOptions).toBe(query.usersGetCurrentUserOptions);
    expect(api.usersGetCurrentUserQueryKey).toBe(query.usersGetCurrentUserQueryKey);
    expect(api.queriesForOperation).toBe(query.queriesForOperation);
  });
});

describe("the profile read", () => {
  it("shares one cache entry with every other current-user read", () => {
    expect(api.usersGetCurrentUserQueryKey()).toEqual(query.usersGetCurrentUserQueryKey());
  });
});
