import { defineConfig } from "vitest/config";

/**
 * Pure string work over a `Request` and an env record, plus the charter guards,
 * which read this package's own manifest and configs off disk — the node
 * environment supplies both `Request` and `readFileSync`, and no browser is
 * booted here.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
