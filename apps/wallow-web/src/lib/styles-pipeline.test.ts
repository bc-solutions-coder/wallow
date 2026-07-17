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
});

describe("the wallow-web Vite Tailwind pipeline", () => {
  it("depends on @tailwindcss/vite", () => {
    const packageJson: { devDependencies?: Record<string, string> } = JSON.parse(
      readFileSync(join(appRoot, "package.json"), "utf8"),
    ) as { devDependencies?: Record<string, string> };

    expect(packageJson.devDependencies?.["@tailwindcss/vite"]).toBeDefined();
  });

  it("registers the @tailwindcss/vite plugin on the production build", () => {
    // Without the plugin, `@import "tailwindcss"` is left verbatim and no
    // utilities are generated — the emitted CSS is inert.
    const names: string[] = pluginNames(viteConfig.plugins);

    expect(names.some((name: string): boolean => name.includes("tailwind"))).toBe(true);
  });

  it("registers the @tailwindcss/vite plugin on the dev host", () => {
    // dev-server.ts drives Vite with `configFile: false`, so it inherits nothing
    // from vite.config.ts — it must wire the Tailwind plugin itself, the same way
    // it already re-declares other Vite options, or `pnpm dev` serves unstyled
    // pages.
    const devServer: string = readFileSync(join(appRoot, "dev-server.ts"), "utf8");

    expect(devServer).toMatch(/@tailwindcss\/vite/u);
  });
});
