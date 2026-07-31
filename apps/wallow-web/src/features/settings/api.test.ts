import { describe, expect, it } from "vitest";

/**
 * Settings (Profile) feature `api.ts` — a THIN RE-EXPORT SEAM over
 * `@bc-solutions-coder/sdk/query`. Profile is READ-ONLY (no mutation
 * endpoint), so the seam exposes only the current-user read.
 *
 * Re-export by IDENTITY is the point: the profile screen and the dashboard's
 * auth guard must read the same generated operation, or a wrapper key gives
 * one resource two cache entries and two requests.
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
