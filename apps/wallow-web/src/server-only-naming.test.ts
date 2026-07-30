import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Every module in this app that reaches a server-only API is named `*.server.*`.
 * That suffix is not decoration: it is the ONLY thing standing between
 * `node:crypto`/`redis`/the SDK's server entry and the browser bundle.
 *
 * TanStack Start's import protection is configured in `vite.config.ts`, and
 * `brand-assets.test.ts` already locks the half of it that sets the IMPORTER
 * scope (`srcDirectory: "src/app"` paired with `importProtection: { include:
 * ["src/**"] }`). This spec locks the other half — WHAT gets denied. The
 * plugin's default client ruleset (start-plugin-core
 * `import-protection/defaults.js`) denies exactly two things: the specifiers
 * `@tanstack/{react,solid,vue}-start/server`, and any imported file matching
 * `**\/*.server.*`. Nothing in {@link SERVER_ONLY_SPECIFIERS} below is on that
 * list by name.
 *
 * So a plainly-named server module is invisible to the guard. This was measured
 * in wallow-web, not assumed (Wallow-v940): while its BFF host was still called
 * `src/app/lib/bff.ts`, a one-line import of it from a `src/shared/` component
 * built clean — exit 0 — and shipped `redis`, `generic-pool` and `createClient`
 * inside a 512 KB browser chunk. Renaming the module to `bff.server.ts` turns
 * the same probe into a hard build failure with a full import trace. The rule
 * fires on the FILENAME, so the convention IS the enforcement, and this spec is
 * what keeps a new server-only module from being added without it. Both zoned
 * apps carry this file identically.
 *
 * Scoped to non-spec sources. Test files legitimately read the filesystem —
 * every app-wide guard spec in this directory opens with `node:fs` — and they
 * never enter a client bundle.
 */

/**
 * Import specifiers that cannot survive in a browser bundle. Prefix matches, so
 * `@bc-solutions-coder/sdk/server` also covers `.../server/passthrough`.
 *
 * `@tanstack/react-start/server` is deliberately absent: Start's default ruleset
 * already denies that one by specifier, so it needs no filename convention.
 */
const SERVER_ONLY_SPECIFIERS: readonly string[] = [
  "node:",
  "redis",
  "openid-client",
  "@bc-solutions-coder/sdk/server",
];

/** Static `from "x"` and dynamic `import("x")` specifiers, in source order. */
const SPECIFIER_PATTERN = /from\s*["']([^"']+)["']|import\(\s*["']([^"']+)["']/gu;

const srcDir: string = dirname(fileURLToPath(import.meta.url));

function sourceFiles(directory: string): readonly string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry): readonly string[] => {
    const full: string = join(directory, entry.name);
    if (entry.isDirectory()) {
      return sourceFiles(full);
    }
    if (!/\.tsx?$/u.test(entry.name)) {
      return [];
    }
    if (/\.(test|spec)\.tsx?$/u.test(entry.name)) {
      return [];
    }
    if (entry.name === "routeTree.gen.ts") {
      return [];
    }
    return [full];
  });
}

function specifiersIn(file: string): readonly string[] {
  const text: string = readFileSync(file, "utf8");
  return [...text.matchAll(SPECIFIER_PATTERN)].map((match): string => match[1] ?? match[2] ?? "");
}

function isServerOnly(specifier: string): boolean {
  return SERVER_ONLY_SPECIFIERS.some((prefix): boolean => specifier.startsWith(prefix));
}

function isServerNamed(file: string): boolean {
  return /\.server\.tsx?$/u.test(file);
}

const SOURCES: readonly string[] = sourceFiles(srcDir);

/** Every non-spec module paired with the server-only specifiers it names. */
const SERVER_REACHING: readonly (readonly [string, readonly string[]])[] = SOURCES.map(
  (file): readonly [string, readonly string[]] => [
    relative(srcDir, file),
    specifiersIn(file)
      .filter((specifier): boolean => isServerOnly(specifier))
      .toSorted(),
  ],
).filter(([, hits]): boolean => hits.length > 0);

describe("server-only module naming", () => {
  // Fail-closed hinge. Every assertion below is satisfied by an empty file list,
  // so a walker that silently stops finding sources would turn this whole spec
  // green while enforcing nothing.
  it("walks a populated source tree", () => {
    expect(SOURCES.length).toBeGreaterThan(20);
  });

  it("names every module that reaches a server-only API `*.server.*`", () => {
    const unnamed: readonly string[] = SERVER_REACHING.filter(
      ([file]): boolean => !isServerNamed(file),
    ).map(([file, hits]): string => `${file} imports ${hits.join(", ")}`);

    expect(unnamed).toEqual([]);
  });

  // The inverse direction, so the suffix keeps meaning what it says. A
  // `*.server.*` module that reaches nothing server-only is either mis-named or
  // a client module the guard is now needlessly refusing to bundle.
  it("gives every `*.server.*` module a server-only reason to exist", () => {
    const decorative: readonly string[] = SOURCES.map((file): string => relative(srcDir, file))
      .filter((file): boolean => isServerNamed(file))
      .filter((file): boolean => !SERVER_REACHING.some(([reaching]): boolean => reaching === file));

    expect(decorative).toEqual([]);
  });
});
