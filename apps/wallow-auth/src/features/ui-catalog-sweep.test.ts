import { readdirSync, readFileSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The completeness half of the ui-catalog sweep (Wallow-m5aq.5.2).
 *
 * The sibling component specs pin what the migrated primitives DO — the tabs'
 * panel wiring and roving tab order, each checkbox's `aria-checked`. Neither
 * they nor any other behavioural test can state the sweep's other criterion:
 * that no hand-rolled COPY of a catalog primitive is left behind somewhere the
 * tests happen not to look. That is a claim about the source, so it is asserted
 * against the source.
 *
 * Two patterns are banned outright under `src/features`, because the
 * 37-component catalog (`@bc-solutions-coder/ui`) covers both and an app-local
 * reimplementation is exactly what this bead removes:
 *
 *   - `type="checkbox"` — the catalog's `Checkbox` owns the box AND the hidden
 *     form input it renders beside it, so app source has no reason to write one.
 *   - `role="tab"` / `role="tablist"` / `role="tabpanel"` — the catalog's `Tabs`
 *     emits all three from Base UI, along with the `aria-controls` /
 *     `aria-labelledby` pairing and the roving tab order that hand-rolled
 *     buttons never get for free.
 *
 * SCOPE, as ruled on the bead: the "no `vi.mock` of `@bc-solutions-coder/ui`"
 * criterion is about app-side STUBS OF MIGRATED PRIMITIVES, not about every
 * appearance of the module name in a `vi.mock` call. The two
 * `src/routes/__root*.test.tsx` specs partial-mock the package
 * (`importOriginal()` spread, one export overridden with a render-nothing
 * sentinel) purely to isolate an SSR render from router context, which is why
 * this scan covers `src/features` and deliberately does not reach them.
 */

const featuresDir: string = fileURLToPath(new URL("./", import.meta.url));

/** Directories a source scan should never have to descend into. */
const ignoredDirs: ReadonlySet<string> = new Set([
  "node_modules",
  "dist",
  ".vite",
  "__screenshots__",
]);

/** Every `.tsx` file under `src/features` that is not itself a spec. */
function findComponentFiles(dir: string): string[] {
  const found: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!ignoredDirs.has(entry.name)) {
        found.push(...findComponentFiles(join(dir, entry.name)));
      }
    } else if (entry.name.endsWith(".tsx") && !entry.name.includes(".test.")) {
      found.push(join(dir, entry.name));
    }
  }
  return found;
}

/** Every `*.test.tsx` spec under `src/features`. */
function findSpecFiles(dir: string): string[] {
  const found: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!ignoredDirs.has(entry.name)) {
        found.push(...findSpecFiles(join(dir, entry.name)));
      }
    } else if (entry.name.endsWith(".test.tsx")) {
      found.push(join(dir, entry.name));
    }
  }
  return found;
}

/** The paths, relative to `src/features`, whose contents match `pattern`. */
function filesMatching(files: readonly string[], pattern: RegExp): string[] {
  return files
    .filter((file) => pattern.test(readFileSync(file, "utf8")))
    .map((file) => relative(featuresDir, file));
}

const componentFiles: string[] = findComponentFiles(featuresDir);
const specFiles: string[] = findSpecFiles(featuresDir);

describe("wallow-auth features: no hand-rolled copies of catalog primitives", () => {
  it("finds component files to scan at all", () => {
    // The guard on the guard: a scan that silently walks an empty tree passes
    // every test below while proving nothing.
    expect(componentFiles.length).toBeGreaterThan(0);
    expect(specFiles.length).toBeGreaterThan(0);
  });

  it("writes no raw checkbox input", () => {
    expect(filesMatching(componentFiles, /type="checkbox"/u)).toEqual([]);
  });

  it("hand-rolls no tab roles", () => {
    expect(filesMatching(componentFiles, /role="tab(list|panel)?"/u)).toEqual([]);
  });

  it("stubs no catalog component", () => {
    // No app-side stub or mock of the catalog: the components under test are the
    // real ones, on real Base UI, exactly as the package's own specs run them.
    expect(filesMatching(specFiles, /vi\.mock\(\s*"@bc-solutions-coder\/ui/u)).toEqual([]);
  });
});
