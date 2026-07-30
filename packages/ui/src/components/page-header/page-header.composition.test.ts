import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/*
 * FOLDER-SHAPE + COMPOSITION SPEC (Wallow-lrlm.3.2). Two claims a render cannot
 * make:
 *
 *   1. The folder ships the full five-file catalog anatomy
 *      (packages/ui/CLAUDE.md). `src/core/package-scaffold.test.ts` covers the
 *      `.tsx`, the `.test.tsx` and the barrel for every component; the recipe
 *      file and the story file are checked here.
 *   2. The title and the description are rendered THROUGH `Text` rather than as
 *      raw tags carrying a hand-rolled class string. The rendered markup of the
 *      two spellings is identical by construction, so — exactly as
 *      `muted-text.composition.test.ts` does for the same claim — the delegation
 *      is asserted against the component's source.
 *
 * This spec reads files and renders nothing, so it runs on the `node` project.
 */

const componentDir = dirname(fileURLToPath(import.meta.url));

/** The five files packages/ui/CLAUDE.md requires of a catalog component folder. */
const REQUIRED_FILES = [
  "page-header.tsx",
  "page-header.styles.ts",
  "page-header.stories.tsx",
  "page-header.test.tsx",
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

describe("PageHeader folder shape", () => {
  it("ships the five-file catalog anatomy", () => {
    const missing = REQUIRED_FILES.filter((name) => !existsSync(join(componentDir, name)));

    expect(missing).toEqual([]);
  });

  it("exports the component and its three recipes from the folder barrel", async () => {
    const barrel = (await import("./index")) as Record<string, unknown>;

    expect(typeof barrel.PageHeader).toBe("function");
    // The recipes stay reachable through `@bc-solutions-coder/ui/page-header`
    // only — the root barrel carries the component and its prop type alone.
    expect(typeof barrel.pageHeaderRecipe).toBe("function");
    expect(typeof barrel.pageHeaderTitleGroupRecipe).toBe("function");
    expect(typeof barrel.pageHeaderActionsRecipe).toBe("function");
  });
});

describe("PageHeader composed over Text", () => {
  it("renders its copy through the Text primitive", () => {
    const source = readComponentCode("page-header.tsx");

    // A sibling import, i.e. the catalog's "deliberate reuse" case — the same
    // shape as MutedText over Text or Label over Field.
    expect(moduleSpecifiers(source).some((specifier) => specifier.startsWith("../text"))).toBe(
      true,
    );
    expect(source).toMatch(/<Text\b/u);
  });

  it("asks Text for a heading element at the title scale", () => {
    const source = readComponentCode("page-header.tsx");

    // `as="h1"` alone derives the `display` scale; the shipped page heading is
    // the `title` scale, so both props have to be spelled out.
    expect(source).toMatch(/as="h1"/u);
    expect(source).toMatch(/variant="title"/u);
  });

  it("takes its layout classes from the recipe file, not from JSX", () => {
    const source = readComponentCode("page-header.tsx");
    const specifiers = moduleSpecifiers(source);

    expect(specifiers).toContain("./page-header.styles");
    // A caller className must be able to win, which only `cn()` over a cva
    // recipe delivers.
    expect(specifiers).toContain("../../core/cn");
    expect(source).toMatch(/\bcn\s*\(/u);
  });

  it("hard-codes no type-scale or colour utility in its JSX", () => {
    const source = readComponentCode("page-header.tsx");

    // The other way to satisfy the rendered-class contract while bypassing
    // Text's recipe: pass the utilities down as a className. The scale and the
    // colour are `Text`'s decisions, spelled as `variant` and `color`.
    for (const utility of [
      "text-3xl",
      "text-sm",
      "font-bold",
      "tracking-tight",
      "text-foreground",
      "text-muted-foreground",
    ]) {
      expect(source, utility).not.toContain(utility);
    }
  });
});
