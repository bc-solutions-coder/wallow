import { describe, expect, it } from "vitest";

/**
 * The apps feature's `api.ts` re-export seam over `@bc-solutions-coder/sdk/query`.
 *
 * A seam that re-declared instead of re-exporting would silently double the
 * surface, so identity is pinned. `queriesWithTag("Apps")` takes a string the
 * compiler cannot check, so it is checked against the real generated key with
 * another feature's key as the negative control.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import { sweeps } from "@bc-solutions-coder/testing/invalidation";
import * as api from "./api";

describe("api.ts re-exports the SDK apps query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.appsGetUserAppsOptions).toBe(query.appsGetUserAppsOptions);
    expect(api.appsGetUserAppsQueryKey).toBe(query.appsGetUserAppsQueryKey);
    expect(api.appsRegisterMutation).toBe(query.appsRegisterMutation);
    expect(api.clientBrandingUpsertBrandingMutation).toBe(
      query.clientBrandingUpsertBrandingMutation,
    );
    expect(api.queriesForOperation).toBe(query.queriesForOperation);
    expect(api.queriesWithTag).toBe(query.queriesWithTag);
  });
});

describe("apps invalidation", () => {
  it("sweeps the app list by tag", () => {
    expect(sweeps(api.queriesWithTag("Apps"), api.appsGetUserAppsQueryKey())).toBe(true);
  });

  it("leaves another feature's queries alone", () => {
    expect(sweeps(api.queriesWithTag("Apps"), query.inquiriesGetAllQueryKey())).toBe(false);
  });

  it("sweeps the app list by operation", () => {
    const appList: ReturnType<typeof api.appsGetUserAppsQueryKey> = api.appsGetUserAppsQueryKey();

    expect(sweeps(api.queriesForOperation(appList), appList)).toBe(true);
  });
});
