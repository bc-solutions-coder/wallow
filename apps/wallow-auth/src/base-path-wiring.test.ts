import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { afterEach, describe, expect, it, vi } from "vitest";

/**
 * The wiring half of base-path support (Wallow-vufu.2.2): `base-path.test.ts`
 * proves the string arithmetic, this file proves the four places that arithmetic
 * has to be plugged in for a `AUTH_BASE_PATH=/auth` build to actually work.
 *
 * Each case below is a failure mode observed in the spike (a real
 * `AUTH_BASE_PATH=/auth` build driven through the built Nitro server), not a
 * hypothetical:
 *
 *  1. **`base` in `vite.config.ts`** — without it every emitted asset URL stays
 *     at the root while the ingress only routes `/auth/*` here.
 *  2. **`nitro`'s `baseURL`** — the subtlest one. Vite's `base` rewrites the URLs
 *     in the HTML to `/auth/assets/*`, but Nitro keeps SERVING `.output/public`
 *     at the root, so the page renders and then every script and stylesheet
 *     404s. The app looks up and never hydrates.
 *  3. **`router.basepath`** for the Start plugin, so the router matches and emits
 *     prefixed paths.
 *  4. **The SDK's `baseUrl`** on both the SSR (`start.ts`) and browser
 *     (`router.tsx`) sides, since the passthrough now answers under the prefix.
 *
 * Node project: it reads files off disk and imports the Vite config; it mounts
 * nothing. The read-the-source cases follow the precedent
 * `routes/passthrough-routes.test.ts` set — what is being asserted is that a
 * particular FILE carries a particular wiring, which importing would paper over.
 */

const srcDir: string = dirname(fileURLToPath(import.meta.url));
const appDir: string = resolve(srcDir, "..");

function readAppFile(...segments: string[]): string {
  return readFileSync(join(appDir, ...segments), "utf8");
}

describe("vite.config.ts", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  async function loadConfig(basePath?: string): Promise<Record<string, unknown>> {
    if (basePath === undefined) {
      vi.stubEnv("AUTH_BASE_PATH", "");
    } else {
      vi.stubEnv("AUTH_BASE_PATH", basePath);
    }
    vi.resetModules();
    const module = (await import("../vite.config")) as { default: Record<string, unknown> };
    return module.default;
  }

  it("serves at the root when AUTH_BASE_PATH is unset — the default fork behavior", async () => {
    const config = await loadConfig();

    // Either spelling is the Vite default; what must NOT happen is a prefix.
    expect(config.base ?? "/").toBe("/");
  });

  it("bases the build on AUTH_BASE_PATH so every emitted asset URL carries the prefix", async () => {
    const config = await loadConfig("/auth");

    expect(config.base).toBe("/auth/");
  });

  it("tolerates the prefix written without slashes, as a compose file will spell it", async () => {
    const config = await loadConfig("auth");

    expect(config.base).toBe("/auth/");
  });

  it("hands the Start plugin a router basepath, not just a Vite base", () => {
    // Read from source: the constructed plugin objects do not expose the options
    // they were built with, so there is nothing on the config to assert against.
    const source: string = readAppFile("vite.config.ts");
    const routerBlock: RegExpMatchArray | null = source.match(/router:\s*\{[\s\S]*?\}/u);

    expect(routerBlock?.[0], "vite.config.ts has no tanstackStart router block").toBeDefined();
    expect(routerBlock?.[0]).toMatch(/basepath/u);
  });

  it("gives nitro the same base, so it SERVES .output/public under the prefix", () => {
    // The one that bites: without this the HTML advertises /auth/assets/* and
    // nitro answers 404 for all of them, so the page never hydrates.
    const source: string = readAppFile("vite.config.ts");
    const nitroCall: RegExpMatchArray | null = source.match(/nitro\(([\s\S]*?)\)/u);

    expect(nitroCall?.[1], "vite.config.ts has no nitro() call").toBeDefined();
    expect(nitroCall?.[1]).toMatch(/baseURL/u);
  });

  it("reads the prefix from AUTH_BASE_PATH and from nowhere else", () => {
    const source: string = readAppFile("vite.config.ts");

    expect(source).toContain("AUTH_BASE_PATH");
  });
});

describe("the SDK base URL", () => {
  it("is the origin PLUS the base path on the SSR side, where the passthrough now answers", () => {
    // src/start.ts builds the per-request SDK from `new URL(request.url).origin`.
    // Under a prefix the bare origin points at whatever the ingress serves at
    // the root — wallow-web — so every SSR-side call leaves this app.
    const source: string = readAppFile("src", "app", "start.ts");

    expect(source).toMatch(/withBasePath|BASE_PATH/u);
  });

  it("is the origin PLUS the base path in the browser too", () => {
    // src/router.tsx mints its own same-origin SDK when there is no request.
    const source: string = readAppFile("src", "app", "router.tsx");

    expect(source).toMatch(/withBasePath|BASE_PATH/u);
  });
});

describe("the Dockerfile", () => {
  const dockerfile: string = readAppFile("Dockerfile");

  it("takes the base path as a build ARG", () => {
    // It cannot be a runtime ENV: the prefix is baked into every asset URL by
    // `vite build`, so a container started with a different value would still
    // serve HTML pointing at the prefix it was BUILT with.
    expect(dockerfile).toMatch(/^ARG AUTH_BASE_PATH/mu);
  });

  it("promotes that ARG to an ENV, since vite reads process.env and not the ARG", () => {
    expect(dockerfile).toMatch(/^ENV AUTH_BASE_PATH=/mu);
  });

  it("sets it BEFORE the build runs, or the build never sees it", () => {
    const envIndex: number = dockerfile.search(/^ENV AUTH_BASE_PATH=/mu);
    const buildIndex: number = dockerfile.indexOf("wallow-auth build");

    expect(envIndex).toBeGreaterThan(-1);
    expect(buildIndex).toBeGreaterThan(-1);
    expect(envIndex).toBeLessThan(buildIndex);
  });
});

/**
 * Internal navigation written as a raw `<a href="/login">` is invisible to the
 * router, so it keeps pointing at the site root under a based build. Locally
 * that merely costs a redirect; behind the path-based ingress this task exists
 * for, `wallow.dev/login` is routed to wallow-web and the user is thrown out of
 * the auth app mid-flow.
 *
 * The fix is per-link: a router `Link` (which applies the basepath) for a route
 * this app owns, or an href built through the base path for anything else.
 */
describe("internal navigation", () => {
  /** Component trees whose links are app-internal navigation. */
  const SCOPED_DIRS: readonly string[] = ["shared/components", "features", "app/routes"];

  /** A literal root-relative href — the form the router cannot rebase. */
  const RAW_INTERNAL_HREF: RegExp = /href="\/[^"]*"/gu;

  function componentSources(): string[] {
    return readdirSync(srcDir, { recursive: true, withFileTypes: true })
      .filter(
        (entry) =>
          entry.isFile() &&
          entry.name.endsWith(".tsx") &&
          !entry.name.endsWith(".test.tsx") &&
          SCOPED_DIRS.some((dir) => relative(srcDir, entry.parentPath).startsWith(dir)),
      )
      .map((entry) => join(entry.parentPath, entry.name));
  }

  it("scans every scoped directory, so a stale name cannot empty the scan", () => {
    // `componentSources()` filters by a path PREFIX, and a prefix that matches
    // nothing yields an empty list, which makes the case below pass green having
    // read no file at all. The zone restructure renamed two of the three (
    // `components/` -> `shared/components/`, `routes/` -> `app/routes/`) and
    // nothing here would have noticed.
    const scanned: readonly string[] = componentSources().map((file: string): string =>
      relative(srcDir, file),
    );

    for (const dir of SCOPED_DIRS) {
      expect(
        scanned.filter((path: string): boolean => path.startsWith(`${dir}/`)).length,
        `${dir}/ contributed no source file`,
      ).toBeGreaterThan(0);
    }
  });

  it("uses no literal root-relative href, so every link survives a base path", () => {
    const offenders: string[] = componentSources().flatMap((file: string): string[] => {
      const matches: string[] = [...readFileSync(file, "utf8").matchAll(RAW_INTERNAL_HREF)].map(
        (match: RegExpExecArray): string => match[0],
      );
      return matches.map((match: string): string => `${relative(srcDir, file)}: ${match}`);
    });

    expect(offenders).toStrictEqual([]);
  });
});
