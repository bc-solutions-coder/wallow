import { defineConfig } from "vitest/config";

/**
 * The package's specs are pure logic over the current-user query options, the
 * role/permission helpers, and the module graph — the node environment is enough
 * and no browser is booted here (same posture as packages/query).
 *
 * `useCurrentUser` is the one export with rendering behaviour, and it is
 * deliberately NOT covered by a browser project of its own: it is a one-line
 * `useQuery(currentUserQuery(client))`, and the interesting part — what a screen
 * does with the loading/anonymous/signed-in states — belongs to the app
 * component suites that own those screens.
 */
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
