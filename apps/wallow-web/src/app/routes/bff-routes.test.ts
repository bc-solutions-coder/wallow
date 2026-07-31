import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Which URL prefixes this origin answers itself, rather than rendering the
 * router into: `/bff/**` (the OIDC tunnel — `/` and `/dashboard/**` both gate on
 * `/bff/user`), `/api/**` (the BFF's own reverse proxy, bearer attached
 * server-side) and `/health` (both compose stacks probe it).
 *
 * Asserted over the file text, not by importing: the contract is that the FILE
 * exists where the route codegen scans, and a missing splat route fails silently
 * as a plain 404 that looks like an ordinary bad URL.
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
