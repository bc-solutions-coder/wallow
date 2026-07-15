import { defineConfig } from "vitest/config";

// tanstack-min entry points (app.ts, server.ts) are browser/server side-effecting
// modules that run at import time, so they are not directly unit-testable. The
// only pure unit under test today is the CSRF safe-method predicate, so the node
// environment suffices; switch to jsdom only if/when DOM-touching units are extracted.
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
