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
