import { defineConfig } from "vitest/config";

/**
 * Run the dependency-free helper tests in Node.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
