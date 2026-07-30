import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/*
 * FOLDER-SHAPE + SOURCE SPEC (Wallow-lrlm.3.5). Three claims a render cannot
 * make:
 *
 *   1. The folder ships the full five-file catalog anatomy
 *      (packages/ui/CLAUDE.md). `src/core/package-scaffold.test.ts` covers the
 *      `.tsx`, the `.test.tsx` and the barrel for every component; the recipe
 *      file and the story file are checked here.
 *   2. The `render` contract comes from `@base-ui/react`'s own `useRender` hook
 *      rather than a second hand-rolled spelling of it. A render that
 *      substitutes an anchor proves *a* render prop exists; it cannot prove the
 *      prop behaves the way every other `render` in this catalog does. `ListRow`
 *      wraps no headless Base UI part, so `useRender` is the only way it gets
 *      the same plumbing.
 *   3. The row's classes come from the recipe file through `cn()`, not from a
 *      string literal in JSX — the only spelling under which a caller's
 *      conflicting `className` actually wins.
 *
 * This spec reads files and renders nothing, so it runs on the `node` project.
 */

const componentDir = dirname(fileURLToPath(import.meta.url));

/** The five files packages/ui/CLAUDE.md requires of a catalog component folder. */
const REQUIRED_FILES = [
  "list-row.tsx",
  "list-row.styles.ts",
  "list-row.stories.tsx",
  "list-row.test.tsx",
  "index.ts",
];

/** The Base UI subpath `useRender` lives on. */
const USE_RENDER_SUBPATH = "@base-ui/react/use-render";

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

describe("ListRow folder shape", () => {
  it("ships the five-file catalog anatomy", () => {
    const missing = REQUIRED_FILES.filter((name) => !existsSync(join(componentDir, name)));

    expect(missing).toEqual([]);
  });

  it("exports the component and its recipe from the folder barrel", async () => {
    const barrel = (await import("./index")) as Record<string, unknown>;

    expect(typeof barrel.ListRow).toBe("function");
    // The recipe stays reachable through `@bc-solutions-coder/ui/list-row`
    // only — the root barrel carries the component and its prop type alone.
    expect(typeof barrel.listRowRecipe).toBe("function");
  });
});

describe("ListRow render contract", () => {
  it("gets its render prop from Base UI's useRender hook", () => {
    const source = readComponentCode("list-row.tsx");

    expect(moduleSpecifiers(source)).toContain(USE_RENDER_SUBPATH);
    // The hook itself, not just its types: a type-only import would leave the
    // behaviour hand-rolled while the signature claimed otherwise.
    expect(source).toMatch(/\buseRender\s*\(/u);
  });

  it("keeps the li as the element it substitutes away from", () => {
    const source = readComponentCode("list-row.tsx");

    // `useRender` defaults to a `<div>`; a row inside `ListCard`'s `<ul>` must
    // default to `<li>`.
    expect(source).toMatch(/defaultTagName:\s*["']li["']/u);
  });

  it("registers the use-render subpath for both browser vitest projects", () => {
    const config = readFileSync(join(componentDir, "..", "..", "..", "vitest.config.ts"), "utf8");

    // Every `@base-ui/react/*` subpath a component imports must be listed in
    // `baseUiSubpaths` or Vite pre-bundles it with its own copy of React and
    // the specs die on `Cannot read properties of null (reading 'useRef')`
    // (packages/ui/CLAUDE.md).
    expect(config).toContain(`"${USE_RENDER_SUBPATH}"`);
  });
});

describe("ListRow source", () => {
  it("takes its classes from the recipe file, not from JSX", () => {
    const source = readComponentCode("list-row.tsx");
    const specifiers = moduleSpecifiers(source);

    expect(specifiers).toContain("./list-row.styles");
    expect(specifiers).toContain("../../core/cn");
    expect(source).toMatch(/\bcn\s*\(/u);
  });

  it("hard-codes no layout or colour utility in its JSX", () => {
    const source = readComponentCode("list-row.tsx");

    for (const utility of ["items-center", "justify-between", "px-6", "py-4", "hover:bg-"]) {
      expect(source, utility).not.toContain(utility);
    }
  });

  it("carries no opacity-suffixed colour in its recipe", () => {
    const recipe = readComponentCode("list-row.styles.ts");

    // The `hover:bg-background/50` the apps hand-roll today is exactly the
    // spelling this epic erases: every colour maps onto one semantic token.
    expect(recipe).not.toMatch(/\bbg-[a-z-]+\/\d+/u);
    expect(recipe).toContain("hover:bg-muted");
  });

  it("derives the row's test id rather than taking one from the caller", () => {
    const source = readComponentCode("list-row.tsx");

    expect(source).toMatch(/-item/u);
  });
});
