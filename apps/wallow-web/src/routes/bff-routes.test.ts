import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The successor to the deleted `proxy-topology.test.ts`.
 *
 * Which URL prefixes this origin answers itself — rather than rendering the
 * router into — is the app's external contract, not an implementation detail.
 * Under the old hosts it was `isBffProxyPath`, a predicate two servers consulted;
 * under Start it is the SET OF ROUTE FILES, so a dropped file is now the way the
 * contract breaks — and it breaks silently, because a missing splat route is a
 * plain 404 that looks like an ordinary bad URL:
 *
 *  - `/bff/**` — the OIDC tunnel. Losing it breaks login and, because `/` and
 *    `/dashboard/**` gate on `/bff/user`, signs every visitor out.
 *  - `/api/**` — the BFF's own reverse proxy (bearer attached server-side), NOT
 *    a verbatim passthrough like wallow-auth's `/v1/**`.
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
  { file: "bff/$.ts", path: "/bff/$" },
  { file: "api/$.ts", path: "/api/$" },
  { file: "health.ts", path: "/health" },
];

describe("the wallow-web BFF routes", () => {
  for (const { file, path } of ROUTES) {
    it(`${file} declares the route path ${path} with a server handler`, () => {
      const source: string = readFileSync(join(routesDir, file), "utf8");

      expect(source).toContain(`createFileRoute("${path}")`);
      expect(source).toMatch(/server:\s*\{\s*handlers:/u);
    });
  }
});
