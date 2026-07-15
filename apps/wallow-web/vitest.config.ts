import { createRequire } from "node:module";
import { defineConfig } from "vitest/config";

// The BFF spike tests (src/lib/bff-server.test.ts) hermetically mock
// `openid-client` (`vi.mock`), then drive the SDK's BFF handlers through
// bff-server.ts. Two harness knobs make that mock reach the SDK's transitive
// `openid-client` import:
//   1. Inline the workspace SDK (realpath `packages/sdk`) so Vitest transforms
//      it instead of loading the built dist via native ESM — a native import
//      would bypass the module mock entirely.
//   2. Alias `openid-client` to its single resolved entry so the specifier the
//      test mocks and the specifier the SDK imports key to the SAME module id
//      (pnpm otherwise resolves it to different ids across packages, so the
//      factory mock would miss). Resolved from the SDK, which owns the dep, so
//      the path tracks version bumps automatically.
const sdkRequire = createRequire(new URL("../../packages/sdk/package.json", import.meta.url));
const openidClientEntry: string = sdkRequire.resolve("openid-client");

export default defineConfig({
  resolve: {
    alias: {
      "openid-client": openidClientEntry,
    },
  },
  test: {
    environment: "node",
    include: ["src/**/*.test.{ts,tsx}"],
    server: { deps: { inline: [/packages[/\\]sdk/u] } },
  },
});
