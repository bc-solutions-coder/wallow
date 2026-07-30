import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Every feature is a bounded context with ONE public entry: its `index.ts`.
 * The zone DAG bans deep imports into a feature from outside it, so a feature
 * without a barrel is a feature nothing can mount.
 *
 * The barrel is also why a feature module may not import `@features/<own name>`:
 * that is a cycle. Reach your own modules relatively.
 *
 * Node project: reads files, mounts nothing.
 */
const srcDir: string = dirname(fileURLToPath(import.meta.url));
const featuresDir: string = resolve(srcDir, "features");

function featureDirs(): readonly string[] {
  return readdirSync(featuresDir, { withFileTypes: true })
    .filter((entry): boolean => entry.isDirectory())
    .map((entry): string => entry.name)
    .toSorted();
}

describe("feature public contracts", () => {
  it("finds the features it is meant to cover", () => {
    expect(featureDirs().length).toBeGreaterThan(0);
  });

  it.each(featureDirs())("features/%s exposes an index.ts barrel", (feature: string) => {
    expect(existsSync(join(featuresDir, feature, "index.ts"))).toBe(true);
  });

  it.each(featureDirs())("features/%s's barrel is re-exports only", (feature: string) => {
    // A barrel with logic in it is a module pretending to be a contract. It also
    // makes the barrel un-tree-shakeable, which matters: every route that mounts
    // one component from a feature pulls the barrel's whole graph into its chunk.
    const code: string = readFileSync(join(featuresDir, feature, "index.ts"), "utf8")
      .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
      .replaceAll(/^\s*\/\/.*$/gmu, "");

    expect(code, `features/${feature}/index.ts imports something`).not.toMatch(/^\s*import\s/mu);
    expect(code, `features/${feature}/index.ts uses export *`).not.toMatch(/export\s*\*/u);
  });

  it.each(featureDirs())("features/%s's barrel reaches only its own modules", (feature: string) => {
    const code: string = readFileSync(join(featuresDir, feature, "index.ts"), "utf8");
    const specifiers: readonly string[] = [...code.matchAll(/\bfrom\s+"([^"]+)"/gu)].map(
      (match): string => match[1] as string,
    );

    expect(specifiers.length, `features/${feature}/index.ts exports nothing`).toBeGreaterThan(0);

    for (const specifier of specifiers) {
      expect(specifier, `features/${feature}/index.ts reaches outside itself`).toMatch(/^\.\//u);
    }
  });
});
