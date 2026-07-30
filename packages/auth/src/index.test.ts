/**
 * Barrel and boundary pins for @bc-solutions-coder/auth (Wallow-x4qn.3).
 *
 * The package exists so an app's auth imports come from ONE place, which makes
 * three things structural rather than incidental:
 *
 *   1. THE SURFACE, in both directions. A dropped export sends the next migration
 *      back to reaching into the SDK (or re-inventing a current-user probe, which
 *      is exactly the duplication this package deletes), and an accidentally
 *      widened one turns a curated surface into a grab bag.
 *   2. IDENTITY of the SDK re-exports. `isAdmin`/`requireAuth`/`loginRedirect` are
 *      re-exported by reference, not wrapped, so app code importing them from here
 *      gets the SDK's own tested guards.
 *   3. NO ROUTER, and NO RAW REACT-QUERY. `useCurrentUser` takes the client as an
 *      argument precisely so this package needs no `@tanstack/react-router`
 *      dependency (the rule `packages/sdk/src/route-context.ts` already follows),
 *      and every react-query symbol arrives through the `@bc-solutions-coder/query`
 *      facade. Both are absences, so they are asserted over the source rather than
 *      by exercising behaviour.
 */

import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { isAdmin, loginRedirect, requireAuth } from "@bc-solutions-coder/sdk";
import { describe, expect, it } from "vitest";

import * as auth from "./index";

/** This spec lives at src/, so ONE level up reaches the package root. */
const packageDir: string = join(dirname(fileURLToPath(import.meta.url)), "..");
const sourceDir: string = join(packageDir, "src");

/**
 * This spec NAMES the specifiers it forbids, so scanning itself would be a
 * permanent false positive.
 */
const SCAN_EXEMPT: string = "index.test.ts";

/** This package's own contribution to the barrel. */
const OWN_EXPORTS: readonly string[] = [
  "currentUserQuery",
  "ensureCurrentUser",
  "hasPermission",
  "hasRole",
  "useCurrentUser",
];

/**
 * The SDK guards and claim helpers re-exported so auth imports come from one
 * package, held next to the bindings they must BE.
 *
 * Named imports rather than a namespace import: the repo-root oxlint
 * `no-restricted-imports` rule bans `import * as` from the SDK, because a
 * namespace import reaches the deleted module-global client symbols too.
 */
const SDK_GUARDS: Readonly<Record<string, unknown>> = { isAdmin, loginRedirect, requireAuth };

const REEXPORTED_FROM_SDK: readonly string[] = Object.keys(SDK_GUARDS);

/** Every `.ts` file under `src/`, minus this spec. */
function sourceFiles(): string[] {
  return readdirSync(sourceDir)
    .filter((entry: string): boolean => entry.endsWith(".ts") && entry !== SCAN_EXEMPT)
    .map((entry: string): string => join(sourceDir, entry));
}

/** The files that import anything from `specifier`, relative to `src/`. */
function importersOf(specifier: string): string[] {
  // No escaping needed: a package specifier carries no regex metacharacters.
  const pattern = new RegExp(`from\\s*["']${specifier}["']`, "u");

  return sourceFiles()
    .filter((file: string): boolean => pattern.test(readFileSync(file, "utf8")))
    .map((file: string): string => file.slice(sourceDir.length + 1));
}

describe("@bc-solutions-coder/auth barrel", () => {
  it("exports the current-user layer plus the re-exported SDK guards, and nothing else", () => {
    expect(Object.keys(auth).toSorted()).toEqual(
      [...OWN_EXPORTS, ...REEXPORTED_FROM_SDK].toSorted(),
    );
  });

  it("exposes its own members as functions", () => {
    for (const name of OWN_EXPORTS) {
      expect(typeof (auth as Record<string, unknown>)[name], name).toBe("function");
    }
  });

  it("re-exports the SDK guards by reference identity rather than wrapping them", () => {
    const authExports = auth as Record<string, unknown>;

    for (const name of REEXPORTED_FROM_SDK) {
      expect(authExports[name], name).toBe(SDK_GUARDS[name]);
    }
  });
});

describe("@bc-solutions-coder/auth boundaries", () => {
  it("has source files to scan", () => {
    // Without this the two absence assertions below could go vacuously green.
    expect(sourceFiles().length).toBeGreaterThan(3);
  });

  it("imports no router — the client is passed in, so this package needs none", () => {
    expect(importersOf("@tanstack/react-router")).toEqual([]);
  });

  it("reaches react-query only through the @bc-solutions-coder/query facade", () => {
    // The facade rule: only packages/query declares react-query. A direct import
    // here would put a second react-query copy in a consumer's graph, and a
    // `useQuery` from copy B inside a provider from copy A throws at runtime.
    expect(importersOf("@tanstack/react-query")).toEqual([]);
    expect(importersOf("@bc-solutions-coder/query").length).toBeGreaterThan(0);
  });
});

describe("@bc-solutions-coder/auth agent guide", () => {
  // The canonical semantics only stay canonical if the next contributor can find
  // them. wallow-auth already shipped a second, subtly different current-user
  // probe once; a rule documented nowhere gets re-invented.
  const guide: string = join(packageDir, "CLAUDE.md");

  it("exists", () => {
    expect(existsSync(guide)).toBe(true);
  });

  it("names every export in its surface table", () => {
    const prose: string = readFileSync(guide, "utf8");

    for (const name of [...OWN_EXPORTS, ...REEXPORTED_FROM_SDK]) {
      expect(prose, name).toMatch(new RegExp(`\\b${name}\\b`, "u"));
    }
  });

  it("states the no-router rule and the canonical current-user semantics", () => {
    const prose: string = readFileSync(guide, "utf8");

    expect(prose).toMatch(/router/iu);
    expect(prose).toMatch(/usersGetCurrentUserQueryKey/u);
    expect(prose).toMatch(/401/u);
    expect(prose).toMatch(/staleTime/u);
    expect(prose).toMatch(/30/u);
    expect(prose).toMatch(/\bsub\b/u);
  });
});
