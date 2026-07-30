import { describe, expect, it } from "vitest";

/**
 * Apps feature `api.ts` — a THIN RE-EXPORT SEAM over
 * `@bc-solutions-coder/sdk/query`. Everything behind it is GENERATED as of
 * Wallow-pu6a.5.5, which changed what this spec can usefully pin:
 *
 *  - re-export identity is still worth pinning (a seam that re-declared instead
 *    of re-exporting would silently double the surface);
 *  - the old key and per-mutation `onSuccess` assertions are not. Generated keys
 *    are flat and built by hey-api, and the mutations no longer carry an
 *    `onSuccess` — invalidation moved to the call sites, where the component
 *    specs assert it against the rendered screen.
 *
 * What DOES still need pinning is the one editorial decision left here: which
 * curated predicate reaches this feature's queries. `queriesWithTag("Apps")`
 * takes a string the compiler cannot check, and a typo would leave every
 * post-registration sweep silently matching nothing — so it is checked against
 * the real generated key, with another feature's key as the negative control.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import { sweeps } from "@shared/testing/invalidation";
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
