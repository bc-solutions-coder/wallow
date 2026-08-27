import { describe, expect, it } from "vitest";

import { assertRouterStubApplied } from "./router-stub";

/**
 * Specs for the stub-applied guard a router-mocking spec runs in `beforeEach`.
 * The marker itself is attached inline in each `vi.mock` factory — a factory
 * cannot reference imports — so what is under test here is only the check.
 */
describe("assertRouterStubApplied", () => {
  it("accepts a component carrying the stub marker", () => {
    const stub = Object.assign(() => null, { wallowRouterStub: true });

    expect(() => {
      assertRouterStubApplied(stub);
    }).not.toThrow();
  });

  it("names the defect when the mock silently served the real module", () => {
    expect(() => {
      assertRouterStubApplied(() => null);
    }).toThrow(/router stub not applied/u);
  });

  it("rejects a value that is not a component at all", () => {
    expect(() => {
      assertRouterStubApplied(undefined);
    }).toThrow(/router stub not applied/u);
  });
});
