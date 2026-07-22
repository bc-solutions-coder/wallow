import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import viteConfig from "../../vite.config";

/**
 * The Tailwind-pipeline half of adopting `@bc-solutions-coder/styles`
 * (Wallow-ffpq.1.2), re-pointed at the shared package's `./vite` seam
 * (Wallow-0q2s.5.2).
 *
 * `@bc-solutions-coder/styles` now owns the whole Tailwind implementation: the
 * `@tailwindcss/vite` plugin and the `publicDir: brandAssetsDir` wiring are both
 * folded into a single `wallowStyles()` factory exported from the package's
 * `./vite` subpath. The app's entire Tailwind surface collapses to two things —
 * a `wallowStyles()` call spread into each Vite pass that serves the app (the
 * production build in `vite.config.ts` and the `configFile: false` dev host in
 * `dev-server.ts`), and a two-line CSS entry that pulls in the package's Tailwind
 * entry AND declares the `@source` scan the package cannot do on the app's
 * behalf.
 *
 * These tests guard that new seam rather than the old raw wiring:
 *   - the app no longer declares its own `@tailwindcss/vite` dependency,
 *   - both Vite passes adopt `wallowStyles()` (the produced plugin list carries
 *     the package's `wallow:brand-assets` plugin, proving the factory ran and not
 *     just a bare `tailwindcss()`),
 *   - the CSS entry is trimmed to exactly the two irreducible lines,
 *   - that entry still imports the package's Tailwind entry and scans this app's
 *     own markup (the silent-failure guard: a package-owned stylesheet cannot see
 *     this app's files, so a missing `@source` ships CSS that styles nothing).
 */

const appRoot: string = fileURLToPath(new URL("../../", import.meta.url));
const srcDir: string = fileURLToPath(new URL("../", import.meta.url));
const authLayout: string = join(srcDir, "components", "auth-layout.tsx");

/** The name the shared package's brand-assets Vite plugin declares. */
const BRAND_ASSETS_PLUGIN = "wallow:brand-assets";

/** Directories a source scan should never have to descend into. */
const ignoredDirs: ReadonlySet<string> = new Set(["node_modules", "dist", ".vite"]);

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

describe("the wallow-auth Tailwind entry", () => {
  it("exists as a CSS file the app owns", () => {
    // The irreducible app-side remainder of the Tailwind pipeline is this entry
    // stylesheet — the one thing the package cannot own because `@source` paths
    // resolve relative to the declaring file.
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
    // nothing — the build stays green and the screens stay bare.
    const entry: string | undefined = cssEntry();
    const css: string = entry === undefined ? "" : readFileSync(entry, "utf8");
    const base: string = entry === undefined ? srcDir : dirname(entry);

    const sources: string[] = [...css.matchAll(/@source\s+["'](?<path>[^"']+)["']/gu)].map(
      (match: RegExpMatchArray): string => resolve(base, match.groups?.path ?? ""),
    );

    expect(sources.some((source: string): boolean => contains(source, authLayout))).toBe(true);
  });

  it("is trimmed to exactly the three-line entry with no explanatory comment", () => {
    // The design moves the explanatory header into the package's own styles.css;
    // the app entry collapses to the irreducible statements. As of Wallow-0q2s.6.1
    // that is THREE lines: the shared Tailwind entry, the `@bc-solutions-coder/ui`
    // `@source` passthrough (so Tailwind scans the ui package's component sources,
    // which live outside this app's own scan and inside skipped node_modules), then
    // this app's own `@source`. Any residual comment or extra rule means the trim
    // did not happen.
    const entry: string | undefined = cssEntry();
    const css: string = entry === undefined ? "" : readFileSync(entry, "utf8");

    expect(css).not.toMatch(/\/\*/u);

    const lines: string[] = css
      .split("\n")
      .map((line: string): string => line.trim())
      .filter((line: string): boolean => line.length > 0);

    expect(lines).toHaveLength(3);
    expect(lines[0]).toMatch(/^@import\s+["']@bc-solutions-coder\/styles\/styles\.css["'];$/u);
    expect(lines[1]).toMatch(/^@import\s+["']@bc-solutions-coder\/ui\/source\.css["'];$/u);
    expect(lines[2]).toMatch(/^@source\s+["']\.\/["'];$/u);
  });
});

describe("the wallow-auth Vite Tailwind pipeline", () => {
  it("no longer declares its own @tailwindcss/vite dependency", () => {
    // The plugin is now a transitive dependency of `@bc-solutions-coder/styles`,
    // supplied through `wallowStyles()`. Listing it here again would let the app
    // drift onto a second, independently-versioned copy of Tailwind.
    const packageJson: {
      dependencies?: Record<string, string>;
      devDependencies?: Record<string, string>;
    } = JSON.parse(readFileSync(join(appRoot, "package.json"), "utf8")) as {
      dependencies?: Record<string, string>;
      devDependencies?: Record<string, string>;
    };

    expect(packageJson.dependencies?.["@tailwindcss/vite"]).toBeUndefined();
    expect(packageJson.devDependencies?.["@tailwindcss/vite"]).toBeUndefined();
  });

  it("adopts wallowStyles() from the shared package in the production build config", () => {
    // The config drops the raw `@tailwindcss/vite` import + explicit
    // `publicDir` line in favour of the single `wallowStyles()` factory from the
    // package's `./vite` subpath. Since Wallow-0q2s.8.5 that wiring is invoked
    // through `@bc-solutions-coder/web-shell`'s `createClientViteConfig()` preset
    // rather than directly in this file, so this delegates to the factory the
    // same way the dev-server test below does.
    const config: string = readFileSync(join(appRoot, "vite.config.ts"), "utf8");

    expect(config).toMatch(/@bc-solutions-coder\/web-shell\/server/u);
    expect(config).toMatch(/createClientViteConfig\s*\(/u);
    expect(config).not.toMatch(/@tailwindcss\/vite/u);
  });

  it("registers the shared brand-assets and tailwind plugins on the production build", () => {
    // Behaviour-level proof that `wallowStyles()` actually ran: the produced
    // plugin list carries BOTH the Tailwind compiler plugin and the package's
    // `wallow:brand-assets` plugin. A bare `tailwindcss()` would give the former
    // but not the latter, so this distinguishes the new seam from the old wiring.
    const names: string[] = pluginNames(viteConfig.plugins);

    expect(names.some((name: string): boolean => name.includes("tailwind"))).toBe(true);
    expect(names).toContain(BRAND_ASSETS_PLUGIN);
  });

  it("delegates the dev host's Tailwind wiring to the shared web-shell factory", () => {
    // dev-server.ts drives Vite with `configFile: false`, so its pass inherits
    // nothing from vite.config.ts and must wire the Tailwind pipeline itself.
    // Since Wallow-0q2s.8.4 that wiring lives in `@bc-solutions-coder/web-shell`'s
    // `createDevServer` factory (which always spreads `wallowStyles()` into the
    // dev Vite instance — proven by that package's dev-server suite), so this file
    // delegates to the factory rather than hand-rolling `tailwindcss()` +
    // `publicDir`. The negative guard stays: it must never grow its own raw
    // `@tailwindcss/vite` import and drift onto a second Tailwind copy.
    const devServer: string = readFileSync(join(appRoot, "dev-server.ts"), "utf8");

    expect(devServer).toMatch(/@bc-solutions-coder\/web-shell\/server/u);
    expect(devServer).toMatch(/createDevServer\s*\(/u);
    expect(devServer).not.toMatch(/@tailwindcss\/vite/u);
  });
});
