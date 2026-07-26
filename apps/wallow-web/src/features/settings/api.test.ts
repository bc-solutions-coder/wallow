import { describe, expect, it } from "vitest";

/**
 * Settings (Profile) feature `api.ts` (Wallow-evd5.2.2) — a THIN RE-EXPORT SEAM
 * over `@bc-solutions-coder/sdk/query`. Profile is READ-ONLY (no mutation
 * endpoint), so the seam exposes only `settingsQueries`. This spec pins the
 * re-export identity and the preserved profile key. The old
 * `vi.mock("../../lib/wallow-sdk")` delegation spec is gone.
 */

import * as api from "./api";
import { settingsQueries } from "./api";
import { queryKeys } from "@bc-solutions-coder/sdk/query";
import * as query from "@bc-solutions-coder/sdk/query";

describe("api.ts re-exports the SDK settings query layer", () => {
  it("re-exports settingsQueries by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.settingsQueries).toBe(query.settingsQueries);
  });
});

describe("settingsQueries", () => {
  it("keys the profile query from the central queryKeys factory", () => {
    expect(settingsQueries.profile().queryKey).toEqual(queryKeys.settings.profile());
  });

  it("keeps the profile queryKey stable across calls", () => {
    expect(settingsQueries.profile().queryKey).toEqual(settingsQueries.profile().queryKey);
  });
});
