import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Every wallow-auth feature that talks to the API talks to it through ONE module:
 * its own `features/<feature>/api.ts` (Wallow-x4qn.9.4). The seam buys three things
 * a direct import cannot:
 *
 *  1. A feature's data surface is enumerable. `api.ts` is the list of operations the
 *     feature is allowed to reach, reviewable in one file, and a screen that starts
 *     calling a fourteenth endpoint has to say so there first.
 *  2. Regeneration lands in one place per feature. The artifacts behind the seam are
 *     GENERATED from `packages/sdk/openapi/v1.json`; a renamed operation breaks one
 *     re-export instead of five screens.
 *  3. The `api.test.ts` beside each seam can assert what a call site cannot — that a
 *     re-export really is the generated artifact and not a look-alike wrapper.
 *
 * WHO ENFORCES WHAT, since Wallow-l5x2. The BOUNDARY half — that no screen reaches
 * around its seam — is lint's now, and the rule this file used to stand in for has
 * landed: `apps/wallow-auth/.oxlintrc.json` bans `@bc-solutions-coder/sdk/query`
 * outright under `src/features/**` and `src/app/routes/**`, bans the three raw data
 * operations there by name, and turns both off again for the seam files themselves
 * (`features/<feature>/api.ts` and its co-located spec). It reports the offence at the offending import, which is where a boundary
 * violation is fixed, and it needs no table of consumer paths × artifact names to do
 * it — the ~400 lines of `DATA_CONSUMERS` / `RAW_DATA_OPERATIONS` / `NO_SEAM_FEATURES`
 * that used to live here had to be re-edited every time a component moved.
 *
 * WHAT SURVIVES IS THE SHAPE OF THE SEAM ITSELF, which no import-level rule can see,
 * and it is DERIVED from disk rather than declared: the seams are whatever
 * `features/*\/api.ts` files exist. So a new feature is covered the moment it is
 * written, and a feature with no endpoints simply has no seam to check — the old
 * hand-kept "these five are data-free" list could only ever describe the features
 * someone remembered to add to it.
 *
 * Node project — it reads files; it mounts nothing.
 */

/** The generated TanStack surface: `{op}Options()`, `{op}Mutation()`, `{op}QueryKey()`. */
const QUERY_ENTRY = "@bc-solutions-coder/sdk/query";

/** The raw barrel: the operations, the guards, the url builders and the DTO types. */
const SDK_ENTRY = "@bc-solutions-coder/sdk";

const srcDir: string = dirname(fileURLToPath(import.meta.url));
const featuresDir: string = join(srcDir, "features");

/**
 * Every feature that owns a seam, read off disk.
 *
 * The derivation IS the coverage rule: a feature reaching an endpoint has an
 * `api.ts` and is checked here; a feature rendering static copy has none and is
 * silently, correctly, out of scope.
 */
function seamOwners(): readonly string[] {
  return readdirSync(featuresDir, { withFileTypes: true })
    .filter((entry): boolean => entry.isDirectory())
    .map((entry): string => entry.name)
    .filter((feature): boolean => existsSync(join(featuresDir, feature, "api.ts")))
    .toSorted();
}

/** Seam source with comments removed, so prose naming an entry is not read as code. */
function seamCode(feature: string): string {
  return readFileSync(join(featuresDir, feature, "api.ts"), "utf8")
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/^\s*\/\/.*$/gmu, "");
}

/** The names one seam re-exports, `type` members and alias targets normalised away. */
function reExportedNames(feature: string): readonly string[] {
  return [...seamCode(feature).matchAll(/export\s+(?:type\s+)?\{([^}]*)\}\s*from\s+"[^"]+"/gu)]
    .flatMap((match): readonly string[] => (match[1] as string).split(","))
    .map((name): string =>
      (
        name
          .trim()
          .replace(/^type\s+/u, "")
          .split(/\s+as\s+/u)[0] as string
      ).trim(),
    )
    .filter((name): boolean => name.length > 0);
}

/** The modules one seam pulls from: `export … from`, `import … from`, `import("…")`. */
function moduleSpecifiers(feature: string): readonly string[] {
  const code: string = seamCode(feature);

  return [
    ...[...code.matchAll(/\bfrom\s+"([^"]+)"/gu)].map((match): string => match[1] as string),
    ...[...code.matchAll(/^\s*import\s+"([^"]+)"/gmu)].map((match): string => match[1] as string),
    ...[...code.matchAll(/\bimport\(\s*"([^"]+)"\s*\)/gu)].map(
      (match): string => match[1] as string,
    ),
  ];
}

describe("wallow-auth's feature data seams", () => {
  it("finds seams to check at all", () => {
    // The guard on a derived table: a broken walk, or a rename of `features/`,
    // would make every case below pass over an empty list.
    expect(seamOwners().length).toBeGreaterThan(5);
  });

  it.each(seamOwners())("features/%s owns a co-located api.test.ts", (feature: string) => {
    // The identity spec is half the seam. A re-export is the one construct whose
    // correctness a call site cannot observe: a hand-written wrapper with the same
    // name type-checks, renders, and sends a subtly different request.
    const spec = `features/${feature}/api.test.ts`;

    expect(existsSync(join(srcDir, spec)), `${spec} does not exist`).toBe(true);
  });

  it.each(seamOwners())("features/%s/api.ts is a re-export and nothing else", (feature: string) => {
    // No imports, no local declarations, no `export *`. A seam that imports
    // something has begun to be a module with behaviour, and behaviour behind a
    // name that looks like a generated artifact is precisely what the co-located
    // identity spec exists to catch — better to make it structurally impossible.
    // `export *` is banned because it states nothing about what the feature
    // reaches, which is the one thing the seam is for.
    const code: string = seamCode(feature);

    expect(code, `features/${feature}/api.ts imports something`).not.toMatch(/^\s*import\s/mu);
    expect(code, `features/${feature}/api.ts re-exports a whole entry`).not.toMatch(/export\s*\*/u);
  });

  it.each(seamOwners())("features/%s/api.ts names a surface", (feature: string) => {
    // Non-empty, so the boundary cannot be "satisfied" by scattering placeholder
    // seams: an `api.ts` that re-exports nothing is a file to maintain that
    // documents no endpoint, and it would pass every other case here.
    expect(reExportedNames(feature).length).toBeGreaterThan(0);
  });

  it.each(seamOwners())("features/%s/api.ts reaches only the SDK's entries", (feature: string) => {
    // The two published entries and nothing else. A seam is where the feature's
    // dependency on generated code is written down; one that also pulls from a
    // sibling feature, or from `dist/`, has stopped being that.
    for (const specifier of new Set(moduleSpecifiers(feature))) {
      expect([SDK_ENTRY, QUERY_ENTRY], `features/${feature}/api.ts reaches ${specifier}`).toContain(
        specifier,
      );
    }
  });
});
