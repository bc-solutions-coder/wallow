import { defineConfig } from "vitest/config";

/**
 * Run the pure environment and URL helper tests in Node.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
