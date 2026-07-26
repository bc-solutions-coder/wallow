import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
    // Vitest 4 narrowed `vi.restoreAllMocks()` (and the specs' `afterEach`) to
    // only restore `vi.spyOn` spies, no longer resetting `vi.fn` module-factory
    // mocks between tests. Reset before each test so per-test implementation
    // overrides and call history on those `vi.fn(actual.*)` mocks do not leak.
    mockReset: true,
  },
});
