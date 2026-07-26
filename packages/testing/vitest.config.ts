import { defineConfig } from "vitest/config";

/**
 * The package itself only exports vitest configuration helpers and thin
 * re-exports, so its OWN specs are pure logic over config objects and package
 * files on disk — the node environment is enough and no browser is booted here.
 * (The browser-mode preset this package PRODUCES is exercised by the consuming
 * apps, not by this package's own suite.)
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
