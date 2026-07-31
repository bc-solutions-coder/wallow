import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The dashboard chrome's colour-token sweep (Wallow-lrlm.5.4) — the SOURCE half
 * of retiring the `bg-foreground`/`text-background` sidebar inversion.
 *
 * WHY A SOURCE GUARD AND NOT ONLY A DOM ONE. The sibling `*.restyle.test.tsx`
 * specs assert what the three nav modes actually paint, but they can only judge
 * the branch they render. A class string that survives in an unrendered branch
 * (or in a `const` no mode reaches at the viewport a case happens to set) is
 * exactly the kind of leftover this bead exists to remove, and it is also what
 * Wallow-lrlm.5.6's lint gate will ban outright. Reading the file is the only
 * assertion that covers every branch at once.
 *
 * WHY THESE TWO FILES ARE SWEPT HERE RATHER THAN IN `src/typography.test.ts`.
 * That spec belongs to the closed Wallow-lrlm.5.3 and deliberately EXEMPTS them
 * (its `SIDEBAR_INVERSION` set) so the two beads never claim the same edit. The
 * exemption was never a licence for the alpha to survive the epic — this file is
 * where it is collected instead, and 5.3's spec is left untouched.
 *
 * THE INVERSION, AND WHAT REPLACES IT. `bg-foreground text-background` is a
 * MECHANICAL inversion: it does not mean "the sidebar surface", it means "swap
 * the two page colours". `@bc-solutions-coder/styles` now names the surface —
 * `--color-sidebar`, `--color-sidebar-foreground`, `--color-sidebar-accent`
 * (Wallow-lrlm.1.1) — so the chrome can say what it means and a fork can rebrand
 * the rail from `branding.json` without touching this app.
 */

const componentsDir: URL = new URL("./", import.meta.url);

function read(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, componentsDir)), "utf8");
}

/**
 * Strip block and line comments before matching, exactly as `typography.test.ts`
 * does: a doc comment that NAMES the class a component no longer hand-rolls is
 * worth keeping, and the guards below would otherwise fail on the prose that
 * explains them.
 */
function code(relativePath: string): string {
  return read(relativePath)
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/\/\/[^\n]*/gu, "");
}

/** The two files that make up the dashboard shell's chrome. */
const CHROME: readonly string[] = ["DashboardNav.tsx", "DashboardLayout.tsx"];

/**
 * The inversion pair, with or without a variant prefix (`hover:`, `md:`) and
 * with or without an opacity modifier. `bg-background` and `text-foreground` are
 * NOT here: the shell's main column legitimately paints itself in the page
 * colours, and only the pair that means "swap them" is retired.
 */
const INVERSION_UTILITY =
  /\b(?:[a-z-]+:)*(?:bg-foreground|text-background|border-background|border-foreground)(?:\/\d{1,3})?\b/gu;

/**
 * A theme colour utility carrying an opacity modifier — the same expression
 * `src/typography.test.ts` sweeps the rest of the app with, copied rather than
 * imported so the closed spec stays a spec and not a helper module.
 */
const ALPHA_COLOR =
  /\b[a-z-]*(?:text|bg|border|ring|divide|fill|stroke)-(?:foreground|background|card|card-foreground|primary|primary-foreground|secondary|muted|muted-foreground|accent|accent-foreground|destructive|border|ring|sidebar|sidebar-foreground|sidebar-accent|success)\/\d{1,3}\b/gu;

/**
 * The ONE occurrence this sweep does not retire: the mobile drawer's dimming
 * scrim in `DashboardLayout`'s `NavBackdrop`.
 *
 * A scrim is not an inversion. Translucency is the whole point of it — a scrim
 * that is not see-through is a blank page — so there is no opaque token that
 * could express it, and the catalog itself reaches for precisely this idiom:
 * `drawerBackdropRecipe` and `alertDialogBackdropRecipe` are both
 * `bg-foreground/50`, and `popoverBackdropRecipe` is the same shape at `/20`
 * (`packages/ui/src/components/*\/*.styles.ts`). Painting it `bg-sidebar` would
 * make the scrim OPAQUE and hide the page it is meant to dim.
 *
 * Named as a single string rather than waved through by a looser regex so that
 * the carve-out is exactly one class on exactly one file, and so Wallow-lrlm.5.6
 * has a literal to exempt when it bans the rest.
 */
const BACKDROP_SCRIM = "bg-foreground/40";

/** `file`'s matches for `pattern`, deduplicated, minus the scrim carve-out. */
function offenders(file: string, pattern: RegExp): string[] {
  return [...new Set(code(file).match(pattern))].filter((cls) => cls !== BACKDROP_SCRIM);
}

describe("the dashboard chrome names its sidebar surface", () => {
  it.each(CHROME)("hard-codes no foreground/background inversion in %s", (file) => {
    expect(
      offenders(file, INVERSION_UTILITY),
      `${file} must paint the sidebar with the sidebar-* tokens, not by swapping the page colours`,
    ).toEqual([]);
  });

  it.each(CHROME)("uses no alpha-modified colour utility in %s", (file) => {
    expect(
      offenders(file, ALPHA_COLOR),
      `${file} must name these colours as tokens, not tint them`,
    ).toEqual([]);
  });

  /*
   * The positive half. The guards above are satisfied by a file that simply
   * deletes its colours, which would leave the rail unpainted rather than
   * migrated — so the nav has to be shown reaching for the family that replaces
   * them. `DashboardLayout` is deliberately absent: its controls sit in the MAIN
   * column, not on the rail, and must NOT take the inverted palette (see
   * `DashboardLayout.restyle.test.tsx`).
   */
  it("paints the nav with the sidebar token family", () => {
    const source = code("DashboardNav.tsx");
    const missing = ["bg-sidebar", "text-sidebar-foreground", "bg-sidebar-accent"].filter(
      (token) => !source.includes(token),
    );

    expect(missing, "the rail, its text and its row states each need their token").toEqual([]);
  });

  /*
   * The scrim carve-out, stated as an assertion rather than left implicit: the
   * one surviving occurrence is the backdrop's and nothing else's, so a future
   * `bg-foreground` cannot slip back in under cover of the exemption.
   */
  it("keeps the drawer scrim as the catalog's own translucent backdrop", () => {
    const layout = code("DashboardLayout.tsx");
    const survivors = [...new Set(layout.match(INVERSION_UTILITY))];

    expect(survivors, "only the drawer scrim may keep a foreground tint").toEqual([BACKDROP_SCRIM]);
    expect(code("DashboardNav.tsx")).not.toContain(BACKDROP_SCRIM);
  });
});
