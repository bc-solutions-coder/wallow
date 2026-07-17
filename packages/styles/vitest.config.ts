import { defineConfig } from "vitest/config";

/**
 * The package renders nothing — it is pure TypeScript over api/branding.json
 * plus a static CSS entry read off disk — so the node environment is enough and
 * no jsdom is pulled in.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
