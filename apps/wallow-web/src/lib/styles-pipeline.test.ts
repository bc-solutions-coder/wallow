import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import viteConfig from "../../vite.config";

/**
 * The Tailwind-pipeline half of adopting `@bc-solutions-coder/styles` in
 * wallow-web (Wallow-ffpq.3.4), mirroring the already-solved wallow-auth version.
 *
 * wallow-web ships ZERO CSS today — no entry stylesheet and no className
 * anywhere — which is exactly why every route renders as unstyled HTML. Adopting
 * the shared package starts with an entry CSS that pulls in the package's
 * Tailwind entry AND scans this app's own markup, plus the `@tailwindcss/vite`
 * plugin registered on every Vite pass that serves the app (the production build
 * in `vite.config.ts` and the dev host in `dev-server.ts`, which runs Vite with
 * `configFile: false` and therefore inherits nothing from the config file).
 *
 * The trap these tests guard: a stylesheet that `@import`s Tailwind but declares
 * no `@source` for the CONSUMING app builds clean and ships CSS while styling
 * nothing — CSS living in a package cannot see this app's files. So it is not
 * enough that an entry exists and imports the package; it must also scan this
 * app's source tree, and the plugin must be present to compile it.
 */

const appRoot: string = fileURLToPath(new URL("../../", import.meta.url));
const srcDir: string = fileURLToPath(new URL("../", import.meta.url));
const dashboardLayout: string = join(srcDir, "components", "DashboardLayout.tsx");

/** Directories a source scan should never have to descend into. */
const ignoredDirs: ReadonlySet<string> = new Set(["node_modules", "dist", ".vite", "public"]);

/** Every `.css` file under the app, excluding build output and dependencies. */
function findCssFiles(dir: string): string[] {
  const found: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!ignoredDirs.has(entry.name)) {
        found.push(...findCssFiles(join(dir, entry.name)));
      }
    } else if (entry.name.endsWith(".css")) {
      found.push(join(dir, entry.name));
    }
  }
  return found;
}

/** The app's Tailwind entry stylesheet, or `undefined` if none exists yet. */
function cssEntry(): string | undefined {
  return findCssFiles(appRoot)[0];
}

/** `true` when `ancestor` is `descendant` or one of its parent directories. */
function contains(ancestor: string, descendant: string): boolean {
  const rel: string = relative(ancestor, descendant);
  return rel === "" || (!rel.startsWith("..") && !rel.startsWith(sep) && !/^\.\.[/\\]/u.test(rel));
}

/** Flatten Vite's nested plugin option tree into the plugin names it declares. */
function pluginNames(plugins: unknown): string[] {
  if (Array.isArray(plugins)) {
    return plugins.flatMap((plugin: unknown): string[] => pluginNames(plugin));
  }
  if (plugins !== null && typeof plugins === "object" && "name" in plugins) {
    const { name }: { name?: unknown } = plugins as { name?: unknown };
    return typeof name === "string" ? [name] : [];
  }
  return [];
}

describe("the wallow-web Tailwind entry", () => {
  it("exists as a CSS file the app owns", () => {
    // The app currently ships zero CSS, which is exactly why every route renders
    // unstyled. Adopting the shared package starts with an entry stylesheet.
    expect(cssEntry()).toBeDefined();
  });

  it("imports the shared styles package's Tailwind entry", () => {
    // `@bc-solutions-coder/styles/styles.css` is the package's `./styles.css`
    // export — the one place `@import "tailwindcss"` and the `@theme` token map
    // live, so consuming it (not re-declaring it) is what keeps branding shared.
    const entry: string | undefined = cssEntry();
    const css: string = entry === undefined ? "" : readFileSync(entry, "utf8");

    expect(css).toMatch(/@import\s+["']@bc-solutions-coder\/styles(?:\/styles\.css)?["']/u);
  });

  it("declares an @source that scans this app's own markup", () => {
    // The silent-failure guard: a package-owned stylesheet cannot see this app's
    // files, so the entry must declare its own @source. A scan that does not
    // cover src/components (where the class names live) ships CSS that styles
    // nothing — the build stays green and the routes stay bare.
    const entry: string | undefined = cssEntry();
    const css: string = entry === undefined ? "" : readFileSync(entry, "utf8");
    const base: string = entry === undefined ? srcDir : dirname(entry);

    const sources: string[] = [...css.matchAll(/@source\s+["'](?<path>[^"']+)["']/gu)].map(
      (match: RegExpMatchArray): string => resolve(base, match.groups?.path ?? ""),
    );

    expect(sources.some((source: string): boolean => contains(source, dashboardLayout))).toBe(true);
  });

  it("is exactly the three-line entry, with no per-app explanatory comment", () => {
    // Adopting `@bc-solutions-coder/styles/vite` collapses the whole app-side
    // Tailwind surface to its irreducible remainder: import the package's Tailwind
    // entry, import `@bc-solutions-coder/ui`'s `@source` passthrough (added in
    // Wallow-0q2s.6.1 so Tailwind scans the ui package's component sources, which
    // live outside this app's own scan and inside skipped node_modules), then
    // declare the `@source` scan the package cannot do on this app's behalf. The
    // explanatory header moves into the shared package's own styles.css (which
    // already carries it), so this file keeps only the three directives — nothing
    // else.
    const entry: string | undefined = cssEntry();
    const css: string = entry === undefined ? "" : readFileSync(entry, "utf8");

    expect(css).not.toContain("/*");
    const lines: string[] = css
      .split("\n")
      .map((line: string): string => line.trim())
      .filter((line: string): boolean => line.length > 0);
    expect(lines).toEqual([
      '@import "@bc-solutions-coder/styles/styles.css";',
      '@import "@bc-solutions-coder/ui/source.css";',
      '@source "./";',
    ]);
  });
});

describe("the wallow-web Vite Tailwind pipeline", () => {
  it("no longer owns the @tailwindcss/vite dependency", () => {
    // The Tailwind toolchain moved into @bc-solutions-coder/styles, which now owns
    // @tailwindcss/vite as a real dependency. The app must not re-declare it in
    // either dependency list, or two packages pin the Tailwind version
    // independently. The shared package that now owns the wiring stays a dep.
    const packageJson: {
      dependencies?: Record<string, string>;
      devDependencies?: Record<string, string>;
    } = JSON.parse(readFileSync(join(appRoot, "package.json"), "utf8")) as {
      dependencies?: Record<string, string>;
      devDependencies?: Record<string, string>;
    };

    expect(packageJson.devDependencies?.["@tailwindcss/vite"]).toBeUndefined();
    expect(packageJson.dependencies?.["@tailwindcss/vite"]).toBeUndefined();
    expect(packageJson.dependencies?.["@bc-solutions-coder/styles"]).toBeDefined();
  });

  it("registers Tailwind on the production build through wallowStyles()", () => {
    // The plugin registration moved behind @bc-solutions-coder/styles/vite's
    // wallowStyles() factory. Since Wallow-0q2s.8.5 that wiring is invoked through
    // `@bc-solutions-coder/web-shell`'s `createClientViteConfig()` preset rather
    // than directly in this file, so vite.config.ts must delegate to that factory
    // — not import @tailwindcss/vite directly — and the flattened plugin tree it
    // produces must still carry the Tailwind plugin, or `@import "tailwindcss"`
    // ships inert.
    const config: string = readFileSync(join(appRoot, "vite.config.ts"), "utf8");

    expect(config).toMatch(/@bc-solutions-coder\/web-shell\/server/u);
    expect(config).toMatch(/createClientViteConfig\s*\(/u);
    expect(config).not.toMatch(/@tailwindcss\/vite/u);

    const names: string[] = pluginNames(viteConfig.plugins);
    expect(names.some((name: string): boolean => name.includes("tailwind"))).toBe(true);
  });

  it("delegates the dev host's Tailwind wiring to the shared web-shell factory", () => {
    // dev-server.ts drives Vite with `configFile: false`, so its pass inherits
    // nothing from vite.config.ts and must wire styling itself. Since
    // Wallow-0q2s.8.4 that wiring lives in `@bc-solutions-coder/web-shell`'s
    // `createDevServer` factory (which always spreads `wallowStyles()` into the
    // dev Vite instance — proven by that package's dev-server suite), so this file
    // delegates to the factory rather than importing Tailwind directly. The
    // negative guard stays: it must never grow its own `@tailwindcss/vite` import
    // and drift onto a second Tailwind copy, or `pnpm dev` serves unstyled pages.
    const devServer: string = readFileSync(join(appRoot, "dev-server.ts"), "utf8");

    expect(devServer).toMatch(/@bc-solutions-coder\/web-shell\/server/u);
    expect(devServer).toMatch(/createDevServer\s*\(/u);
    expect(devServer).not.toMatch(/@tailwindcss\/vite/u);
  });
});
