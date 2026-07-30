/*
 * Facade pin for @bc-solutions-coder/query.
 *
 * This package is the ONE place TanStack Query enters the workspace: every
 * consumer — apps, packages/forms, packages/testing, packages/auth — imports
 * `useQuery`, `QueryClientProvider`, `QueryClient` and friends from here, never
 * from `@tanstack/react-query` directly (a repo-root oxlint no-restricted-imports
 * rule enforces that separately; this spec enforces that the facade actually
 * carries the whole surface, which no lint rule can see).
 *
 * Two failure modes make that worth pinning by reference identity rather than by
 * name:
 *
 *   1. A NAMED re-export list would silently lag react-query. A consumer that
 *      needed a symbol nobody had listed yet would reach for the raw package,
 *      and the facade would erode one import at a time. So the assertion is
 *      "every runtime export is reachable", derived from the installed package
 *      rather than from a hand-kept list.
 *   2. Identity, not just presence. Two copies of react-query in one graph give
 *      two `QueryClientProvider` React contexts, and a `useQuery` from copy B
 *      inside a provider from copy A throws at runtime ("No QueryClient set").
 *      `facade[name] === tanstack[name]` is what proves the facade re-exports the
 *      same bindings instead of wrapping or re-instantiating them.
 *
 * Pure-logic spec: it only walks the module graph, so it runs in the node
 * project (`vitest.config.ts` is node-only for this package).
 */

import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import * as tanstack from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

import * as facade from "./index";

// This spec lives at src/, so ONE level up reaches the package root.
const packageDir = join(dirname(fileURLToPath(import.meta.url)), "..");

/**
 * Every runtime export of `@tanstack/react-query`, derived from the installed
 * package. `default` is dropped because `export * from` never re-exports a
 * default binding — that is an ES semantics rule, not an omission.
 */
const TANSTACK_RUNTIME_EXPORTS: string[] = Object.keys(tanstack)
  .filter((name: string): boolean => name !== "default")
  .toSorted();

/** What this package adds on top of the re-exported react-query surface. */
const FACADE_ADDITIONS: string[] = ["createQueryClient"];

describe("@bc-solutions-coder/query facade surface", () => {
  it("has a non-empty react-query surface to mirror", () => {
    // Without this the derived cases below could go vacuously green: an empty
    // export list would satisfy "every export is reachable" while the facade
    // re-exported nothing at all.
    expect(TANSTACK_RUNTIME_EXPORTS.length).toBeGreaterThan(20);
    expect(TANSTACK_RUNTIME_EXPORTS).toContain("QueryClient");
    expect(TANSTACK_RUNTIME_EXPORTS).toContain("QueryClientProvider");
    expect(TANSTACK_RUNTIME_EXPORTS).toContain("useQuery");
    expect(TANSTACK_RUNTIME_EXPORTS).toContain("useMutation");
  });

  it("re-exports every react-query runtime export by reference identity", () => {
    const facadeExports = facade as Record<string, unknown>;
    const tanstackExports = tanstack as Record<string, unknown>;

    for (const name of TANSTACK_RUNTIME_EXPORTS) {
      expect(facadeExports[name], name).toBe(tanstackExports[name]);
    }
  });

  it("exports react-query's surface plus createQueryClient and nothing else", () => {
    // Both directions: a dropped re-export fails, and so does an accidentally
    // widened surface — this package is a facade, not a grab bag.
    expect(Object.keys(facade).toSorted()).toEqual(
      [...TANSTACK_RUNTIME_EXPORTS, ...FACADE_ADDITIONS].toSorted(),
    );
  });

  it("adds createQueryClient, which react-query itself does not have", () => {
    // The one symbol that is genuinely ours. Asserting its absence upstream is
    // what makes the previous case's arithmetic honest.
    expect(typeof (facade as Record<string, unknown>).createQueryClient).toBe("function");
    expect(tanstack).not.toHaveProperty("createQueryClient");
  });
});

describe("@bc-solutions-coder/query built package", () => {
  // `dist/` is a build artifact and `pnpm check` runs `test` BEFORE `build`, so a
  // fresh clone has none when these execute. Skipped rather than failed in that
  // case: run `pnpm --filter @bc-solutions-coder/query build` to arm them.
  const distDir = join(packageDir, "dist");
  const distEntry = join(distDir, "index.js");
  const distTypes = join(distDir, "index.d.ts");
  const distIsMissing = !existsSync(distEntry);

  it.skipIf(distIsMissing)(
    "emits a bundle whose runtime surface matches the source barrel",
    async () => {
      // Consumers import the BUILT entry the exports map names, never `src/`. The
      // Vite lib build externalizes every bare specifier, so react-query must
      // still arrive through a live `export *` in the emitted graph — if it were
      // bundled instead, this list would still match while consumers silently got
      // a second react-query copy.
      const built = (await import(pathToFileURL(distEntry).href)) as Record<string, unknown>;

      expect(Object.keys(built).toSorted()).toEqual(Object.keys(facade).toSorted());

      // The anti-bundling guard, and the reason it names react-query's symbols
      // rather than ours: an externalized `export *` yields the SAME bindings the
      // installed package holds, so identity survives the build. A bundled second
      // copy would not — it would satisfy the key-set check above and still hand
      // consumers a rival `QueryClientProvider` context ("No QueryClient set").
      // `createQueryClient` cannot serve here: it is this package's own code, which
      // the lib build inlines into the bundle by definition, so it is a distinct
      // function object across the src/dist boundary no matter what.
      expect(built.QueryClient).toBe(tanstack.QueryClient);
      expect(built.useQuery).toBe(tanstack.useQuery);
      expect(typeof built.createQueryClient).toBe("function");
    },
  );

  it.skipIf(distIsMissing)("declares the facade in the emitted types", () => {
    // `vite build` and `tsc -p tsconfig.build.json` are two independent passes
    // over the same barrel, so the .d.ts can lag the .js. Consumers typecheck
    // against this file alone: it must carry both the react-query re-export and
    // our own symbol.
    const declarations = readFileSync(distTypes, "utf8");

    expect(declarations).toMatch(/@tanstack\/react-query/u);
    for (const name of FACADE_ADDITIONS) {
      expect(declarations, name).toMatch(new RegExp(`\\b${name}\\b`, "u"));
    }
  });
});

describe("@bc-solutions-coder/query agent guide", () => {
  // The facade rule only holds if the next contributor can find it. Pinned on
  // disk because a rule documented nowhere gets re-litigated by every migration.
  const guide = join(packageDir, "CLAUDE.md");

  it("exists", () => {
    expect(existsSync(guide)).toBe(true);
  });

  it("states the facade rule and what the package owns", () => {
    const prose = readFileSync(guide, "utf8");

    expect(prose).toMatch(/facade/iu);
    expect(prose).toMatch(/@tanstack\/react-query/u);
    expect(prose).toMatch(/createQueryClient/u);
    expect(prose).toMatch(/devtools/iu);
  });
});
