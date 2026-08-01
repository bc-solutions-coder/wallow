import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Which URL prefixes this origin forwards to the API — the app's external
 * contract, and under Start it is the SET OF ROUTE FILES. A dropped file breaks
 * it silently: a missing splat route is a plain 404 that reads as a bad URL, so
 * losing `/.well-known/**` breaks login with no useful error.
 *
 * Asserted over the file text rather than by importing the modules: the point is
 * that the FILE sits at the path the route codegen scans, which is exactly what
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
  // Not a passthrough: the browser logger's ingest route, answered here rather
  // than forwarded. It is in this list because it is part of the same contract —
  // a set of paths this origin answers itself.
  { file: "logs.ts", path: "/logs" },
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
