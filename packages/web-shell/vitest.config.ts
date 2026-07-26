import { defineConfig } from "vitest/config";

/**
 * The package exports a standalone host + Vite/dev-server config presets, so its
 * OWN specs are pure logic over config objects and package files on disk — the
 * node environment is enough and no browser is booted here.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
