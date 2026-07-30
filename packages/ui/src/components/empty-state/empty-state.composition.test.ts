import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import {
  emptyStateActionRecipe,
  emptyStateIconRecipe,
  emptyStateRecipe,
} from "./empty-state.styles";

/*
 * FOLDER-SHAPE + COMPOSITION SPEC (Wallow-lrlm.3.3). Three claims a render
 * cannot make:
 *
 *   1. The folder ships the full five-file catalog anatomy
 *      (packages/ui/CLAUDE.md). `src/core/package-scaffold.test.ts` covers the
 *      `.tsx`, the `.test.tsx` and the barrel for every component; the recipe
 *      file and the story file are checked here.
 *   2. The surface is the real `Card` and the copy renders through `Text`,
 *      rather than either being re-spelled as a local class string. The rendered
 *      markup of the two spellings is identical by construction, so — exactly as
 *      `muted-text.composition.test.ts` does for the same claim — the delegation
 *      is asserted against the component's source.
 *   3. The recipes carry layout only. A colour utility here would put the
 *      `text-foreground/60` this component exists to erase back within reach.
 *
 * This spec reads files and calls three recipes, and renders nothing, so it runs
 * on the `node` project.
 */

const componentDir = dirname(fileURLToPath(import.meta.url));

/** The five files packages/ui/CLAUDE.md requires of a catalog component folder. */
const REQUIRED_FILES = [
  "empty-state.tsx",
  "empty-state.styles.ts",
  "empty-state.stories.tsx",
  "empty-state.test.tsx",
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

describe("EmptyState folder shape", () => {
  it("ships the five-file catalog anatomy", () => {
    const missing = REQUIRED_FILES.filter((name) => !existsSync(join(componentDir, name)));

    expect(missing).toEqual([]);
  });

  it("exports the component and its three recipes from the folder barrel", async () => {
    const barrel = (await import("./index")) as Record<string, unknown>;

    expect(typeof barrel.EmptyState).toBe("function");
    // The recipes stay reachable through `@bc-solutions-coder/ui/empty-state`
    // only — the root barrel carries the component and its prop type alone.
    expect(typeof barrel.emptyStateRecipe).toBe("function");
    expect(typeof barrel.emptyStateIconRecipe).toBe("function");
    expect(typeof barrel.emptyStateActionRecipe).toBe("function");
  });
});

describe("EmptyState composed over Card and Text", () => {
  it("renders its surface through the Card component", () => {
    const source = readComponentCode("empty-state.tsx");

    // A sibling import, i.e. the catalog's "deliberate reuse" case — the same
    // shape as MutedText over Text or Label over Field.
    expect(moduleSpecifiers(source).some((specifier) => specifier.startsWith("../card"))).toBe(
      true,
    );
    expect(source).toMatch(/<Card\b/u);
    // The spacing block goes in through Card's own slot, so a caller className
    // still merges last and wins.
    expect(source).toMatch(/spacing=/u);
  });

  it("renders its copy through the Text primitive", () => {
    const source = readComponentCode("empty-state.tsx");

    expect(moduleSpecifiers(source).some((specifier) => specifier.startsWith("../text"))).toBe(
      true,
    );
    expect(source).toMatch(/<Text\b/u);
  });

  it("asks Text for a heading element at the subheading scale", () => {
    const source = readComponentCode("empty-state.tsx");

    // `as="h2"` alone derives the `title` scale; the shipped message is the
    // `subheading` scale, so both props have to be spelled out.
    expect(source).toMatch(/as="h2"/u);
    expect(source).toMatch(/variant="subheading"/u);
    // The description's colour is a token, never an opacity suffix.
    expect(source).toMatch(/color="muted"/u);
  });

  it("takes its layout classes from the recipe file, not from JSX", () => {
    const source = readComponentCode("empty-state.tsx");
    const specifiers = moduleSpecifiers(source);

    expect(specifiers).toContain("./empty-state.styles");
    // A caller className must be able to win, which only `cn()` over a cva
    // recipe delivers — here through Card, which merges spacing then className.
    expect(source).toMatch(/emptyStateRecipe\s*\(/u);
  });

  it("hard-codes no surface, type-scale or colour utility in its JSX", () => {
    const source = readComponentCode("empty-state.tsx");

    // The other way to satisfy the rendered-class contract while bypassing the
    // two components' recipes: pass the utilities down as a className. The
    // surface is `Card`'s decision; the scale and the colour are `Text`'s,
    // spelled as `variant` and `color`.
    for (const utility of [
      "bg-card",
      "border-border",
      "rounded-lg",
      "text-xl",
      "font-semibold",
      "text-foreground",
      "text-muted-foreground",
    ]) {
      expect(source, utility).not.toContain(utility);
    }
  });
});

/** Every semantic colour token `@bc-solutions-coder/styles` defines a scale for. */
const COLOUR_TOKENS = [
  "accent",
  "background",
  "border",
  "card",
  "destructive",
  "foreground",
  "input",
  "muted",
  "popover",
  "primary",
  "ring",
  "secondary",
  "sidebar",
  "success",
];

/** Every class the three recipes emit, flattened. */
function recipeClasses(): string[] {
  return [
    ...emptyStateRecipe().split(" "),
    ...emptyStateIconRecipe().split(" "),
    ...emptyStateActionRecipe().split(" "),
  ];
}

describe("EmptyState recipes", () => {
  it("carries layout only — no colour utility in any recipe", () => {
    // A colour here would be a second place colour is decided, and the first
    // step back towards the `text-foreground/60` the call sites ship today.
    const coloured = recipeClasses().filter((name) =>
      COLOUR_TOKENS.some((token) => name.endsWith(`-${token}`) || name.includes(`-${token}-`)),
    );

    expect(coloured).toEqual([]);
  });

  it("carries no opacity-suffixed utility in any recipe", () => {
    expect(recipeClasses().filter((name) => name.includes("/"))).toEqual([]);
  });

  it("leaves the card surface to cardRecipe", () => {
    // The spacing block replaces Card's `p-6 space-y-6`; it must not restate the
    // surface, or a Card restyle would stop reaching this component.
    for (const utility of ["rounded-lg", "border", "bg-card"]) {
      expect(emptyStateRecipe().split(" "), utility).not.toContain(utility);
    }
  });
});
