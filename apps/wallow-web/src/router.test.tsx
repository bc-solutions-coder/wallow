import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

import { createRouter } from "./router";

/**
 * Boot wiring (Wallow-8w1h.2.2: the Start app boots). `createRouter()` must
 * assemble the root + index routes into a TanStack router so that "/" resolves.
 */
describe("createRouter (boot wiring)", () => {
  it("constructs a router without throwing", () => {
    expect(() => createRouter()).not.toThrow();
  });

  it("registers the index route at /", () => {
    const router = createRouter();
    expect(Object.keys(router.routesById)).toContain("/");
  });
});

/**
 * Query wiring (Wallow-8w1h.3.1). `createRouter()` must register a QueryClient
 * in the router context so route loaders/beforeLoad can reach it via
 * `context.queryClient`. The client comes from `createQueryClient()` and is
 * minted per router (per SSR request), so no arg is required at the call site.
 */
describe("createRouter (query context)", () => {
  it("exposes a QueryClient on the router context", () => {
    const router = createRouter();
    expect(router.options.context?.queryClient).toBeInstanceOf(QueryClient);
  });
});
