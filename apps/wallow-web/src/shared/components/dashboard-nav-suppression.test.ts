import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The nav rows must stop out-merging the catalog by name (Wallow-lrlm.6.4).
 *
 * A destination row is a `NavigationMenu.Link`, so the class attribute that
 * reaches the DOM is `twMerge(navigationMenuLinkRecipe(), itemClass)`. Since the
 * recipe painted from the PAGE palette, `itemClass` had to name a conflicting
 * class for each of the recipe's colours — and `twMerge` drops a class only when
 * the caller conflicts with it AT THE SAME VARIANT, so an unmodified
 * `text-sidebar-foreground` left `hover:text-accent-foreground` standing and a
 * hovered label went to 1.27:1 in light mode (Wallow-lrlm.5.4). The fix then was
 * to add the missing suppressor. That is not a mechanism, it is a list a future
 * edit drops an entry from, silently, with the whole suite still green.
 *
 * WHY A SOURCE GUARD AND NOT A DOM ONE. This is the one criterion no rendered
 * pixel can express: a row that suppresses the recipe correctly paints EXACTLY
 * like a row that never received a page colour to suppress. The sibling
 * `DashboardNav.sidebar-surface.test.tsx` measures what the rail paints and reads
 * the merged classList; `packages/ui/src/sidebar-surface.test.ts` proves the
 * catalog hands over a clean list. Only reading this file can say the consumer
 * stopped compensating. Same instrument, and the same reasoning, as the sibling
 * `dashboard-chrome-tokens.test.ts`.
 *
 * WHY A HOVER *TEXT* COLOUR IS THE SIGNATURE. A row's rest colour already applies
 * while the cursor is over it — restating it under `hover:` changes nothing on
 * screen. The only thing such a class ever does here is out-rank a catalog class
 * at the `hover:` variant. Hover BACKGROUNDS are not swept: a hover surface is a
 * real state change, and wherever it stays in this file (the Sign Out `<button>`
 * is not a catalog component and owns its own row treatment) it is paint, not
 * compensation.
 */

const componentsDir: URL = new URL("./", import.meta.url);

/**
 * Strip block and line comments before matching, exactly as
 * `dashboard-chrome-tokens.test.ts` does: the doc comments in this shell NAME the
 * classes it no longer hand-rolls, and that prose is worth keeping.
 */
function code(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, componentsDir)), "utf8")
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

describe("DashboardNav row colour", () => {
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
    expect(
      hoverTextColours(code("DashboardNav.tsx")),
      "a hover text colour here exists only to out-merge the catalog recipe",
    ).toEqual([]);
  });
});
