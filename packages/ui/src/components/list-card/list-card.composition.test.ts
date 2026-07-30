import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/*
 * FOLDER-SHAPE + SOURCE SPEC (Wallow-lrlm.3.5). Two claims a render cannot make:
 *
 *   1. The folder ships the full five-file catalog anatomy
 *      (packages/ui/CLAUDE.md). `src/core/package-scaffold.test.ts` covers the
 *      `.tsx`, the `.test.tsx` and the barrel for every component; the recipe
 *      file and the story file are checked here.
 *   2. The surface's classes come from the recipe file through `cn()`, not from
 *      a string literal in JSX — the only spelling under which a caller's
 *      conflicting `className` actually wins.
 *
 * This spec reads files and renders nothing, so it runs on the `node` project.
 */

const componentDir = dirname(fileURLToPath(import.meta.url));

/** The five files packages/ui/CLAUDE.md requires of a catalog component folder. */
const REQUIRED_FILES = [
  "list-card.tsx",
  "list-card.styles.ts",
  "list-card.stories.tsx",
  "list-card.test.tsx",
  "index.ts",
];

/**
 * A source file's code, with its comments removed: the component's docstring
 * quotes the utilities it must not hard-code, and prose is documentation rather
 * than something the component does.
 */
function readComponentCode(fileName: string): string {
  return readFileSync(join(componentDir, fileName), "utf8")
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/^\s*\/\/.*$/gmu, "");
}

/** Every module specifier a source file imports or re-exports from. */
function moduleSpecifiers(source: string): string[] {
  const specifiers: string[] = [];

  for (const match of source.matchAll(/(?:\bfrom|^\s*import)\s+["']([^"']+)["']/gmu)) {
    const specifier = match[1];
    if (specifier !== undefined) {
      specifiers.push(specifier);
    }
  }

  return specifiers;
}

describe("ListCard folder shape", () => {
  it("ships the five-file catalog anatomy", () => {
    const missing = REQUIRED_FILES.filter((name) => !existsSync(join(componentDir, name)));

    expect(missing).toEqual([]);
  });

  it("exports the component and both recipes from the folder barrel", async () => {
    const barrel = (await import("./index")) as Record<string, unknown>;

    expect(typeof barrel.ListCard).toBe("function");
    // The recipes stay reachable through `@bc-solutions-coder/ui/list-card`
    // only — the root barrel carries the component and its prop type alone.
    expect(typeof barrel.listCardRecipe).toBe("function");
    expect(typeof barrel.listCardListRecipe).toBe("function");
  });
});

describe("ListCard source", () => {
  it("takes its classes from the recipe file, not from JSX", () => {
    const source = readComponentCode("list-card.tsx");
    const specifiers = moduleSpecifiers(source);

    expect(specifiers).toContain("./list-card.styles");
    // A caller className must be able to win, which only `cn()` over a cva
    // recipe delivers.
    expect(specifiers).toContain("../../core/cn");
    expect(source).toMatch(/\bcn\s*\(/u);
  });

  it("hard-codes no surface utility in its JSX", () => {
    const source = readComponentCode("list-card.tsx");

    for (const utility of ["bg-card", "rounded-lg", "shadow-sm", "border-border", "divide-y"]) {
      expect(source, utility).not.toContain(utility);
    }
  });

  it("derives the list's test id rather than taking one from the caller", () => {
    const source = readComponentCode("list-card.tsx");

    // Derived, never hand-passed — the same rule packages/forms applies to a
    // field catalog, so an app names the list once and its selectors follow.
    expect(source).toMatch(/-table/u);
  });
});
