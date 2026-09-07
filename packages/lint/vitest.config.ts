import { defineConfig } from "vitest/config";

/** Run the fixture suite in Node against the real oxlint binary. */
export default defineConfig({
  test: {
    include: ["src/**/*.test.ts"],
  },
});
