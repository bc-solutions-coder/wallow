import { defineConfig } from "vitest/config";

/**
 * The package exports the TanStack Query facade and the browser-safe query client
 * factory, whose specs are pure logic over the module graph and the returned
 * client's default options — the node environment is enough and no browser is
 * booted here.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
