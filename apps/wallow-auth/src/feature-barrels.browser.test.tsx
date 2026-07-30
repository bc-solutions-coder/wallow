import { describe, expect, it } from "vitest";

/**
 * The other half of `feature-barrels.test.ts`. That file does the SHAPE checks
 * off disk on the node project; this one loads each barrel for real, in
 * Chromium, because the modules behind it are React components.
 *
 * It has to be a SEPARATE `.tsx` file rather than a case in the node spec: the
 * shared preset routes projects by extension, so a `.test.ts` is always on node
 * and `nodeTsxSpecs` only ever pushes `.tsx` the other way. Folded into the node
 * file, this case would evaluate the whole feature graph (Base UI, lucide-react,
 * the @bc-solutions-coder/ui subpaths) under `environment: "node"`.
 *
 * `import.meta.glob` rather than `readdirSync` + a dynamic template specifier:
 * this project has no `node:fs`, and a glob is statically analysable, so Vite
 * pre-bundles the barrels instead of discovering them mid-run (the reload that
 * flakes the browser project).
 */
const barrels: Record<string, () => Promise<Record<string, unknown>>> = import.meta.glob<
  Record<string, unknown>
>("./features/*/index.ts");

describe("feature barrels load", () => {
  it("finds the barrels the node-side spec asserts exist", () => {
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
