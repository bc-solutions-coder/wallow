import { beforeEach, describe, expect, it } from "vitest";

import {
  ensureQueryBootstrapped,
  registerQueryBootstrap,
  resetQueryBootstrapForTests,
} from "./bootstrap";

describe("query bootstrap", () => {
  beforeEach(() => {
    resetQueryBootstrapForTests();
  });

  it("runs the registered configurator exactly once across many ensures", () => {
    let runs = 0;
    registerQueryBootstrap(() => {
      runs += 1;
    });
    ensureQueryBootstrapped();
    ensureQueryBootstrapped();
    expect(runs).toBe(1);
  });

  it("is a no-op when nothing is registered", () => {
    expect(() => ensureQueryBootstrapped()).not.toThrow();
  });

  it("re-arms when a new configurator is registered", () => {
    let first = 0;
    let second = 0;
    registerQueryBootstrap(() => {
      first += 1;
    });
    ensureQueryBootstrapped();
    registerQueryBootstrap(() => {
      second += 1;
    });
    ensureQueryBootstrapped();
    expect(first).toBe(1);
    expect(second).toBe(1);
  });
});
