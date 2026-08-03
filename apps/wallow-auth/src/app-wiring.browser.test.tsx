import { assertBrowserModeSmoke } from "@bc-solutions-coder/testing/browser-mode-smoke";
import { assertThemeWiring } from "@bc-solutions-coder/testing/theme-wiring";
import { describe, expect, it } from "vitest";

/**
 * The browser half of `app-wiring.test.ts`: the project really is Chromium, the
 * stylesheet and fork theme are attached to it, and every feature barrel loads.
 *
 * The barrel loader has to be a `.tsx` file — the shared preset routes projects by
 * extension, so folded into the node file it would evaluate the whole feature graph
 * (Base UI, lucide-react, the `@bc-solutions-coder/ui` subpaths) under
 * `environment: "node"`.
 */

assertBrowserModeSmoke("wallow-auth");

/**
 * Every screen here paints onto a card, so `--card` is the surface that matters
 * alongside the page `--background` and its `--foreground` pair.
 */
assertThemeWiring({
  tokens: ["--background", "--foreground", "--card"],
  probeClass: "bg-card text-card-foreground",
});

/**
 * `import.meta.glob` rather than `readdirSync` + a dynamic template specifier: this
 * project has no `node:fs`, and a glob is statically analysable, so Vite pre-bundles
 * the barrels instead of discovering them mid-run (the reload that flakes the
 * browser project).
 */
const barrels: Record<string, () => Promise<Record<string, unknown>>> = import.meta.glob<
  Record<string, unknown>
>("./features/*/index.ts");

describe("feature barrels load", () => {
  it("finds at least one barrel to load", () => {
    // Without this, a glob that matched nothing would make every case below
    // vacuously absent and the file would pass green having tested nothing.
    expect(Object.keys(barrels).length).toBeGreaterThan(0);
  });

  it.each(Object.keys(barrels).toSorted())("%s resolves every name it exports", async (path) => {
    const module: Record<string, unknown> = await (
      barrels[path] as () => Promise<Record<string, unknown>>
    )();

    expect(Object.keys(module).length).toBeGreaterThan(0);
  });
});
