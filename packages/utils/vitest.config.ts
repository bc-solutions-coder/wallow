import { defineConfig } from "vitest/config";

/**
 * Pure functions over plain values plus the charter guards, which read this
 * package's own manifest and configs off disk — the node environment is enough
 * and no browser is booted here.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
