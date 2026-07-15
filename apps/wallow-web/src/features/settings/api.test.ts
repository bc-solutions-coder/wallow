import { beforeEach, describe, expect, it, vi } from "vitest";

/**
 * Settings (Profile) feature query layer (Wallow-8w1h.6.1) — copies the
 * Organizations api.test.ts spec: the getWallowSdk() facade is mocked and these
 * tests assert the query layer's KEY STABILITY and its DELEGATION to the
 * `settings` slice, not the wire.
 *
 * RECONCILIATION (scout CRITICAL DIVERGENCE #1): profile is READ-ONLY — the
 * bead's "profile mutation" has no backend endpoint (the generic key/value
 * settings PUT is a different concern the oracle never uses for profile), so
 * there is only a `profile()` query and no updateProfileMutation to test.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spy.
const mocks = vi.hoisted(() => ({
  getProfile: vi.fn(),
}));

// Route/component files import only from this feature's api.ts; api.ts in turn
// imports getWallowSdk. We mock the facade so the settings slice is a spy.
vi.mock("../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    settings: {
      getProfile: mocks.getProfile,
    },
  }),
}));

import { settingsQueries } from "./api";

/** Invoke a queryOptions `queryFn` while ignoring its QueryFunctionContext arg. */
async function callQueryFn(queryFn: unknown): Promise<unknown> {
  return (queryFn as () => Promise<unknown>)();
}

describe("settingsQueries", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("profile", () => {
    it("keys the profile query as ['settings', 'profile']", () => {
      expect(settingsQueries.profile().queryKey).toEqual(["settings", "profile"]);
    });

    it("keeps the profile queryKey stable across calls", () => {
      expect(settingsQueries.profile().queryKey).toEqual(settingsQueries.profile().queryKey);
    });

    it("queryFn delegates to the facade settings.getProfile and returns its data", async () => {
      const profile = {
        id: "u1",
        email: "a@b.c",
        firstName: "Ada",
        lastName: "Lovelace",
        roles: ["Owner"],
        permissions: [],
      };
      mocks.getProfile.mockResolvedValue(profile);

      const result = await callQueryFn(settingsQueries.profile().queryFn);

      expect(mocks.getProfile).toHaveBeenCalledTimes(1);
      expect(result).toBe(profile);
    });
  });
});
