import { defineConfig } from "vitest/config";

/**
 * The package exports only the browser-safe TanStack Query client factory, whose
 * specs are pure logic over the returned client's default options — the node
 * environment is enough and no browser is booted here.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
