import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The successor to the deleted `proxy-paths.test.ts`.
 *
 * Which URL prefixes this origin forwards to the API is the app's external
 * contract, not an implementation detail. Under the old host it was a list in
 * `proxy-paths.ts`; under Start it is the SET OF ROUTE FILES, so a dropped file
 * is now the way the contract breaks — and it breaks silently, because a missing
 * splat route is a plain 404 that looks like an ordinary bad URL:
 *
 *  - `/.well-known/**` — OIDC discovery + JWKS. The documents the API publishes
 *    advertise URLs on THIS origin, so losing it breaks login with no useful error.
 *  - `/connect/**` — the OpenIddict authorize/token/logout/userinfo endpoints.
 *  - `/v1/**` — the API surface the screens call.
 *  - `/health` — not a proxy, but the same kind of contract: both compose stacks
 *    probe it, and a container that fails its healthcheck never joins the stack.
 *
 * Asserted over the file text rather than by importing the modules: the point is
 * that the FILE exists at the path the route codegen scans, which is exactly what
 * an import would paper over.
 */

const routesDir: string = dirname(fileURLToPath(import.meta.url));

/** Route files, and the route path each one must claim. */
const ROUTES: ReadonlyArray<{ readonly file: string; readonly path: string }> = [
  { file: "v1/$.ts", path: "/v1/$" },
  { file: "connect/$.ts", path: "/connect/$" },
  // `[.]` is the codegen's escape for a leading dot; a literal `.well-known/`
  // directory would be read as a route-path separator.
  { file: "[.]well-known/$.ts", path: "/.well-known/$" },
  { file: "health.ts", path: "/health" },
];

describe("the wallow-auth passthrough routes", () => {
  for (const { file, path } of ROUTES) {
    it(`${file} declares the route path ${path} with a server handler`, () => {
      const source: string = readFileSync(join(routesDir, file), "utf8");

      expect(source).toContain(`createFileRoute("${path}")`);
      expect(source).toMatch(/server:\s*\{\s*handlers:/u);
    });
  }
});
