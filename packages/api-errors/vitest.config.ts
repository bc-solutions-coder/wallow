import { defineConfig } from "vitest/config";

/**
 * Pure functions over plain values, `Error`s and `Response`s — the node
 * environment is enough and no browser is booted here.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
