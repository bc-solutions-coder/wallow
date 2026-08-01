import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The two criteria about this package's source that no rendered pixel can
 * express.
 *
 * A nav row that suppresses the catalog recipe correctly paints EXACTLY like a
 * row that never received a page colour to suppress — so only reading
 * `app-nav.tsx` can say the rows stopped compensating. And a `const` holding a
 * hand-rolled control string still sits in `app-shell.tsx` for the next edit to
 * pick up even once nothing renders it, while an import spelled against the root
 * `@bc-solutions-coder/ui` barrel breaks every spec here without changing a
 * single rendered class.
 *
 * The DOM halves live next door: `app-nav.sidebar-surface.test.tsx` measures
 * what the rail paints, `app-shell.catalog-control.test.tsx` reads the merged
 * class attribute on both controls.
 */

const sourceDir: URL = new URL("./", import.meta.url);

/**
 * Strip block and line comments before matching: the doc comments in these
 * modules NAME the classes they no longer hand-roll, and that prose is the
 * record of why.
 */
function code(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, sourceDir)), "utf8")
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/\/\/[^\n]*/gu, "");
}

/**
 * A hover text colour, whatever token it names and whatever prefixes precede it
 * (`md:hover:text-…`), with or without an opacity modifier. `hover:text-sm` is
 * not matched — the token has to look like a colour name, not a scale step.
 */
const HOVER_TEXT_COLOUR = /\bhover:text-[a-z][a-z-]*[a-z](?:\/\d{1,3})?\b/gu;

/** Tailwind's type scale, the only `text-*` values that are not colours. */
const TYPE_SCALE: ReadonlySet<string> = new Set([
  "xs",
  "sm",
  "base",
  "lg",
  "xl",
  "left",
  "center",
  "right",
  "wrap",
  "nowrap",
  "balance",
  "pretty",
]);

/** Every `hover:text-<colour>` utility in `source`. */
function hoverTextColours(source: string): readonly string[] {
  return [...source.matchAll(HOVER_TEXT_COLOUR)]
    .map(([match]: RegExpExecArray): string => match)
    .filter((match: string): boolean => !TYPE_SCALE.has(match.slice("hover:text-".length)));
}

/**
 * The utilities that make up a hand-rolled outline button, each of which
 * `buttonRecipe` owns once the control is a `Button`.
 *
 * `text-foreground` and `bg-background` are deliberately ABSENT: the shell's own
 * `<main>` and root paint themselves in those page colours and legitimately keep
 * saying so. Only the utilities that exist to draw a BUTTON are swept — its
 * padding, its radius, its border, its type scale, its weight and its hover fill.
 */
const HAND_ROLLED_BUTTON_UTILITIES: readonly string[] = [
  "px-3",
  "py-2",
  "rounded-md",
  "border-border",
  "text-sm",
  "font-medium",
  "hover:bg-muted",
];

/**
 * Every hand-rolled button utility still present in `source`.
 *
 * Bounded on both sides by "not a class-name character" rather than by `\b`:
 * `\b` treats `-` as a boundary, so `rounded-md` would match inside
 * `rounded-md-ish` and a plain substring check would match it inside a longer
 * token too. None of the swept utilities contains a regex metacharacter, so they
 * are interpolated as written.
 */
function handRolledButtonUtilities(source: string): readonly string[] {
  return HAND_ROLLED_BUTTON_UTILITIES.filter((utility: string): boolean =>
    new RegExp(String.raw`(?<![\w-])${utility}(?![\w-])`, "u").test(source),
  );
}

describe("AppNav row colour", () => {
  it("recognises a suppressor and ignores a type-scale utility", () => {
    // Demonstrated rather than trusted: a matcher that found nothing would turn
    // the case below into a spec that cannot fail.
    expect(
      hoverTextColours(
        'const c = "hover:bg-sidebar-accent hover:text-sidebar-foreground hover:text-sm md:hover:text-accent-foreground/80";',
      ),
    ).toEqual([
      "hover:text-sidebar-foreground",
      // The `md:` prefix is not part of the match, but the utility under it is.
      "hover:text-accent-foreground/80",
    ]);
  });

  it("names no hover text colour, because the recipe owns the row's colour", () => {
    // A row is a `NavigationMenu.Link`, so what reaches the DOM is
    // `twMerge(navigationMenuLinkRecipe(), itemClass)` — and `twMerge` drops a
    // class only where the caller conflicts with it AT THE SAME VARIANT. A row
    // restating its rest colour under `hover:` changes nothing on screen; the
    // only thing such a class ever does is out-rank a catalog class.
    expect(
      hoverTextColours(code("app-nav.tsx")),
      "a hover text colour here exists only to out-merge the catalog recipe",
    ).toEqual([]);
  });
});

describe("the shell's nav controls come from the catalog", () => {
  it("recognises a hand-rolled button utility and ignores the page's own colours", () => {
    // Demonstrated rather than trusted: a sweep that matched nothing would make
    // both cases below pass on any file at all. `text-foreground` and
    // `bg-background` are the trap — they are page colours the shell keeps — and
    // `rounded-md-ish` must not read as `rounded-md`.
    expect(
      handRolledButtonUtilities(
        'const c = "relative z-20 mb-4 px-3 py-2 rounded-md border border-border text-sm ' +
          'font-medium text-foreground hover:bg-muted"; const d = "bg-background rounded-md-ish";',
      ),
    ).toEqual(HAND_ROLLED_BUTTON_UTILITIES);
  });

  it("hand-rolls no button colour, border or padding string", () => {
    expect(
      handRolledButtonUtilities(code("app-shell.tsx")),
      "the catalog's outline Button owns these — the shell must not restate them",
    ).toEqual([]);
  });

  it("declares no shared control class string", () => {
    // A `const` can survive the DOM guard by simply going unused, which is how a
    // deleted style string comes back: the next control reaches for the one
    // already sitting in the file.
    expect(
      /\bnavControlClass\b/u.test(code("app-shell.tsx")),
      "the shared control string is replaced by the catalog Button, not kept beside it",
    ).toBe(false);
  });

  it("imports Button from its per-component subpath, not the root barrel", () => {
    const source: string = code("app-shell.tsx");

    // The root barrel pulls in `FocusOnNavigate`, which calls `useRouterState`,
    // and every spec in this package stubs `@tanstack/react-router` down to
    // `Link`. A bundler tree-shakes the barrel; a dev/test module graph does
    // not, so the barrel form fails to link here — without changing a single
    // rendered class, which is why no DOM assertion can catch it.
    expect(
      source,
      "@bc-solutions-coder/ui/button is the subpath this package's specs can link",
    ).toMatch(/from "@bc-solutions-coder\/ui\/button"/u);
    expect(
      source,
      "the root barrel drags FocusOnNavigate into the router-stubbed graph",
    ).not.toMatch(/from "@bc-solutions-coder\/ui"/u);
  });

  it("asks for the outline variant at both call sites", () => {
    const source: string = code("app-shell.tsx");
    const outlineProps: readonly string[] = [...source.matchAll(/variant="outline"/gu)].map(
      ([match]: RegExpExecArray): string => match,
    );

    // Two controls, two call sites. Rendering one through the catalog and
    // leaving the other hand-rolled is exactly the drift this collapsed.
    expect(outlineProps, "both NavToggle and MobileMenuButton are outline Buttons").toHaveLength(2);
  });
});
