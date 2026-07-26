import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

/**
 * Apps feature `api.ts` (Wallow-evd5.2.2) — a THIN RE-EXPORT SEAM over
 * `@bc-solutions-coder/sdk/query`. This spec pins the seam (re-export identity)
 * and the preserved key/invalidation behavior. The adoption also surfaces
 * `upsertBrandingMutation` from the SDK, which the hand-rolled layer lacked.
 * The old `vi.mock("../../lib/wallow-sdk")` delegation spec is gone.
 */

import * as api from "./api";
import { appsQueries } from "./api";
import { queryKeys } from "@bc-solutions-coder/sdk/query";
import * as query from "@bc-solutions-coder/sdk/query";

/** A QueryClient whose invalidateQueries records the keys it was asked to sweep. */
function captureInvalidations(): { client: QueryClient; keys: unknown[] } {
  const client = new QueryClient();
  const keys: unknown[] = [];
  client.invalidateQueries = (filters?: { queryKey?: unknown }) => {
    keys.push(filters?.queryKey);
    return Promise.resolve();
  };
  return { client, keys };
}

describe("api.ts re-exports the SDK apps query layer", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.appsQueries).toBe(query.appsQueries);
    expect(api.registerAppMutation).toBe(query.registerAppMutation);
    expect(api.upsertBrandingMutation).toBe(query.upsertBrandingMutation);
  });
});

describe("appsQueries", () => {
  it("keys every option from the central queryKeys factory", () => {
    expect(appsQueries.list().queryKey).toEqual(queryKeys.apps.all);
    expect(appsQueries.detail("c1").queryKey).toEqual(queryKeys.apps.detail("c1"));
  });

  it("keeps the list queryKey stable across calls", () => {
    expect(appsQueries.list().queryKey).toEqual(appsQueries.list().queryKey);
  });
});

describe("apps mutation invalidation", () => {
  it("registerAppMutation sweeps the apps list", () => {
    const { client, keys } = captureInvalidations();
    api.registerAppMutation(client).onSuccess();
    expect(keys).toEqual([queryKeys.apps.all]);
  });

  it("upsertBrandingMutation sweeps that app's detail", () => {
    const { client, keys } = captureInvalidations();
    api.upsertBrandingMutation(client, "c1").onSuccess();
    expect(keys).toEqual([queryKeys.apps.detail("c1")]);
  });
});
