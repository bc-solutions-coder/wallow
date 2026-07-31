import { defineConfig } from "vitest/config";

// Node only. Every spec here shells out to the real oxlint binary and reads its
// JSON report; nothing renders, so there is no browser project.
export default defineConfig({
  test: {
    include: ["src/**/*.test.ts"],
  },
});
