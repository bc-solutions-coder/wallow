import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SDK wiring for the minimal reference app (Wallow-pu6a.5.5).
 *
 * minimal-app deliberately renders NO live data — `HelloCard` is static. What it
 * owes a fork is the *wiring*, and that wiring changed shape: the guarded
 * `getSdk()` singleton in `src/lib/sdk.ts` (and the query layer's
 * `registerQueryBootstrap` hook it armed) is deleted. A fork now gets ONE SDK
 * instance PER REQUEST from `createWallowSdk` in `src/start.ts`, lifted into the
 * router context by `src/router.tsx` — which is what a route or component reads
 * it from.
 *
 * This spec pins that skeleton by reading the two files, because there is nothing
 * left to unit-test: with no singleton there is no configure-once guard, no
 * bootstrap ordering, and no module-scope side effect to arm.
 *
 * THE BASE URL IS THE REQUEST ORIGIN, NOT `/api`: this app's server routes
 * passthrough-proxy `/v1/**`, `/connect/**`, and `/.well-known/**` verbatim at
 * the root (see `src/lib/api-passthrough.ts`), so — unlike wallow-web's BFF token
 * tunnel, which mounts under `/api` — the origin serving the page is the origin
 * the API answers on.
 */

const here: string = dirname(fileURLToPath(import.meta.url));
const appRoot: string = resolve(here, "..");

const read = (relativePath: string): string => readFileSync(resolve(appRoot, relativePath), "utf8");

describe("per-request SDK wiring", () => {
  it("mints the SDK inside request middleware, never at module scope", () => {
    const source: string = read("src/start.ts");

    expect(source).toMatch(/createMiddleware\(\)\.server\(/u);
    expect(source).toMatch(/createWallowSdk\(\{/u);
    expect(source).toMatch(/next\(\{\s*context:\s*\{\s*sdk\s*\}\s*\}\)/u);
  });

  it("gives that instance the request's own origin and cookie", () => {
    const source: string = read("src/start.ts");

    expect(source).toMatch(/baseUrl:\s*requestOrigin/u);
    expect(source).toMatch(/cookieHeader:\s*request\.headers\.get\("cookie"\)/u);
  });

  it("lifts the request's SDK into the router context, falling back in the browser", () => {
    const source: string = read("src/router.tsx");

    expect(source).toMatch(/getGlobalStartContext\(\)\?\.sdk\s*\?\?\s*createWallowSdk\(/u);
    expect(source).toMatch(/context:\s*\{\s*queryClient,\s*sdk\s*\}/u);
  });

  it("no longer ships a singleton facade module", () => {
    // Deleted with the SDK's `createConfiguredOnce`/`registerQueryBootstrap`
    // seams. A fork copying this app must not find a second, process-wide way to
    // configure the client sitting next to the per-request one.
    expect(existsSync(resolve(appRoot, "src/lib/sdk.ts"))).toBe(false);
  });

  it("routes and components reach the SDK through context, not a module import", () => {
    // The two entries that legitimately construct one: the server handler and the
    // router that lifts it into context. Everywhere else reads it back out.
    const builders: ReadonlySet<string> = new Set(["src/start.ts", "src/router.tsx"]);

    for (const [entry, source] of appSources().filter(([name]) => !builders.has(name))) {
      expect(source, `${entry} builds its own SDK instance`).not.toMatch(/createWallowSdk\s*\(/u);
    }
  });
});

/**
 * Every non-spec source file in `src`, comments stripped, so the guards below
 * read EXECUTABLE code only. The wiring is documented by prose that names the
 * very hooks the guards forbid ("a fork's first `useQuery(...)`"), and a mention
 * in a comment is not a live query.
 */
function appSources(): readonly (readonly [string, string])[] {
  const stripComments = (source: string): string =>
    source.replaceAll(/\/\*[\s\S]*?\*\//gu, "").replaceAll(/\/\/[^\n]*/gu, "");

  return readdirSync(resolve(appRoot, "src"), { recursive: true })
    .map(String)
    .filter((entry: string) => /\.tsx?$/u.test(entry) && !/\.test\.tsx?$/u.test(entry))
    .map(
      (entry: string) =>
        [`src/${entry}`, stripComments(read(`src/${entry}`))] as readonly [string, string],
    );
}

describe("minimal-app stays query-free while documenting the query layer", () => {
  it("adds no live queries — the wiring is the deliverable, not a demo fetch", () => {
    for (const [entry, source] of appSources()) {
      expect(source, entry).not.toMatch(/\buseSuspenseQuery\s*\(/u);
      expect(source, entry).not.toMatch(/\buseQuery\s*\(/u);
      expect(source, entry).not.toMatch(/\buseMutation\s*\(/u);
    }
  });

  it("README documents the SDK's ./query layer and links the frontend-state guide", () => {
    const readme: string = read("README.md");

    expect(readme).toMatch(/`?\.\/query`?/u);
    expect(readme).toMatch(/frontend-state\.md/u);
  });

  it("every relative doc link in the README resolves to a real file", () => {
    const readme: string = read("README.md");
    const targets: string[] = [...readme.matchAll(/\]\((\.\.?\/[^)\s#]+)/gu)].map(
      (match: RegExpMatchArray) => match[1] as string,
    );

    expect(targets.length).toBeGreaterThan(0);
    for (const target of targets) {
      expect(existsSync(resolve(appRoot, target)), `broken README link: ${target}`).toBe(true);
    }
  });
});
