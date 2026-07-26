import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

import { createQueryClient } from "./query-client";

/**
 * QueryClient factory (moved from apps/{wallow-auth,wallow-web}/src/lib/
 * query-client.ts in Wallow-0q2s.8.2). `createQueryClient()` is the single
 * source of the React Query client wired into the router context and the
 * `__root` `QueryClientProvider`. It must apply an explicit query policy
 * (retry disabled — deterministic tests, no silent backoff) and must mint a
 * fresh client per call so an SSR request never shares cache with another.
 */
describe("createQueryClient", () => {
  it("returns a QueryClient instance", () => {
    expect(createQueryClient()).toBeInstanceOf(QueryClient);
  });

  it("disables query retries by default", () => {
    const client = createQueryClient();
    expect(client.getDefaultOptions().queries?.retry).toBe(false);
  });

  it("mints a fresh client on every call (SSR request isolation)", () => {
    expect(createQueryClient()).not.toBe(createQueryClient());
  });
});
