import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { aliasDirs, resolveAlias } from "../aliases";

/**
 * `aliases.ts` is the single source for this app's three zone aliases, and
 * `vite.config.ts` / `vitest.config.ts` both import it — so those two cannot
 * drift. `tsconfig.json` is JSON and cannot import anything, so it is a
 * hand-maintained mirror, and this spec is its lock.
 *
 * Same idiom as `docker-workspace-copies.test.ts`: read both artifacts off disk
 * and assert the mirror, rather than abstracting the duplication into shared
 * build machinery (which would couple every app to a build package).
 *
 * Node project: reads files, mounts nothing.
 */
const appDir: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");

/** `tsconfig.json` carries `//` comments — strip them before parsing. */
function readTsconfigPaths(): Record<string, string[]> {
  const text: string = readFileSync(resolve(appDir, "tsconfig.json"), "utf8").replaceAll(
    /^\s*\/\/.*$/gmu,
    "",
  );
  const config = JSON.parse(text) as { compilerOptions?: { paths?: Record<string, string[]> } };

  return config.compilerOptions?.paths ?? {};
}

/**
 * The two build configs must actually CONSUME the map, or it is decoration and
 * the mirror it pins is meaningless.
 */
function readsAliasModule(file: string): boolean {
  return /from\s+"\.\/aliases"/u.test(readFileSync(resolve(appDir, file), "utf8"));
}

describe("the zone alias map", () => {
  it("declares exactly the three zones", () => {
    expect(Object.keys(aliasDirs).toSorted()).toEqual(["@app", "@features", "@shared"]);
  });

  it("is mirrored entry-for-entry by tsconfig.json paths", () => {
    const paths: Record<string, string[]> = readTsconfigPaths();

    expect(Object.keys(paths).toSorted()).toEqual(
      Object.keys(aliasDirs)
        .map((key): string => `${key}/*`)
        .toSorted(),
    );

    for (const [key, dir] of Object.entries(aliasDirs)) {
      expect(paths[`${key}/*`], `tsconfig paths has no ${key}/*`).toEqual([`./${dir}/*`]);
    }
  });

  it("resolves each alias to an absolute directory inside this app", () => {
    for (const [key, dir] of Object.entries(aliasDirs)) {
      expect(resolveAlias[`${key}/`]).toBe(`${resolve(appDir, dir)}/`);
    }
  });

  it.each(["vite.config.ts", "vitest.config.ts"])("%s imports the alias map", (file: string) => {
    expect(readsAliasModule(file)).toBe(true);
  });
});
