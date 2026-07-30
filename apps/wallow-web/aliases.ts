import { fileURLToPath } from "node:url";

/**
 * The three zone aliases, as plain data.
 *
 * This app's `vite.config.ts` and `vitest.config.ts` both import this module and
 * derive `resolve.alias` from it, so the runtime and the test runner cannot
 * disagree. `tsconfig.json` cannot import it — JSON — so `src/alias-map.test.ts`
 * pins its `compilerOptions.paths` to this map instead.
 *
 * Deliberately NOT a shared build-config package: a preset would mean deep-rooted
 * build files coupling every app to a package, for three lines of data.
 *
 * `@app` maps to `src/app`, not `src`. An `@app/* -> src/*` entry would overlap
 * the other two and give two spellings for the same module.
 */
export const aliasDirs = {
  "@app": "src/app",
  "@features": "src/features",
  "@shared": "src/shared",
} as const;

/**
 * Vite/vitest `resolve.alias`, keyed WITH the trailing slash.
 *
 * Vite's object-form alias matches by prefix, so a bare `@app` key would also
 * swallow a future `@application`. `@app/` cannot.
 */
export const resolveAlias: Record<string, string> = Object.fromEntries(
  Object.entries(aliasDirs).map(([key, dir]): [string, string] => [
    `${key}/`,
    `${fileURLToPath(new URL(dir, import.meta.url))}/`,
  ]),
);
