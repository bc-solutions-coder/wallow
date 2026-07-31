import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * The dashboard shell stops hand-rolling its nav control (Wallow-lrlm.6.5) — the
 * SOURCE half of moving both controls onto the catalog `Button`.
 *
 * `DashboardLayout.tsx` declares `navControlClass`, an outline button written out
 * by hand and shared by `NavToggle` and `MobileMenuButton`. The catalog already
 * ships that control: `buttonRecipe`'s `outline` arm is the same border-with-no-
 * surface treatment, and it is the arm the app's copy has already drifted from
 * (`hover:bg-muted` where the recipe says `hover:bg-accent
 * hover:text-accent-foreground` — Wallow-lrlm.5.4 shipped the divergence knowingly
 * because both readings were within visual noise of what they replaced).
 *
 * WHY A SOURCE GUARD ON TOP OF THE DOM ONE. The sibling
 * `DashboardLayout.catalog-control.test.tsx` reads the MERGED class attribute and
 * can prove no hand-rolled utility reached the element — but a `const` that no
 * longer feeds a control still leaves the string in the file for the next edit to
 * pick up, and an import spelled against the root barrel breaks every spec in this
 * directory without changing a single rendered class. Neither is visible from the
 * DOM. Same instrument, and the same reasoning, as the sibling
 * `dashboard-chrome-tokens.test.ts` and `dashboard-nav-suppression.test.ts`.
 */

const componentsDir: URL = new URL("./", import.meta.url);

/**
 * Strip block and line comments before matching, exactly as the two sibling
 * source guards do: the doc comments in this shell NAME the classes it no longer
 * hand-rolls, and that prose is the record of why.
 */
function code(relativePath: string): string {
  return readFileSync(fileURLToPath(new URL(relativePath, componentsDir)), "utf8")
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/\/\/[^\n]*/gu, "");
}

/**
 * The utilities that make up the hand-rolled control, each of which the recipe
 * owns once the control is a `Button`.
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

describe("the dashboard nav control comes from the catalog", () => {
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
      handRolledButtonUtilities(code("DashboardLayout.tsx")),
      "the catalog's outline Button owns these — the shell must not restate them",
    ).toEqual([]);
  });

  it("declares no navControlClass", () => {
    // The `const` is the thing the bead retires. It can survive the DOM guard
    // above by simply going unused, which is how a deleted style string comes
    // back: the next control reaches for the one already sitting in the file.
    expect(
      /\bnavControlClass\b/u.test(code("DashboardLayout.tsx")),
      "the shared control string is replaced by the catalog Button, not kept beside it",
    ).toBe(false);
  });

  it("imports Button from its per-component subpath, not the root barrel", () => {
    const source: string = code("DashboardLayout.tsx");

    // The same constraint `DashboardNav.tsx` documents at its own import: the
    // root barrel pulls in `FocusOnNavigate`, which calls `useRouterState`, and
    // every spec in this directory stubs `@tanstack/react-router` down to `Link`
    // plus `Outlet`. A bundler tree-shakes the barrel; a dev/test module graph
    // does not, so the barrel form fails to link here — without changing a single
    // rendered class, which is why no DOM assertion can catch it.
    expect(
      source,
      "@bc-solutions-coder/ui/button is the subpath this directory's specs can link",
    ).toMatch(/from "@bc-solutions-coder\/ui\/button"/u);
    expect(
      source,
      "the root barrel drags FocusOnNavigate into the router-stubbed graph",
    ).not.toMatch(/from "@bc-solutions-coder\/ui"/u);
  });

  it("asks for the outline variant at both call sites", () => {
    const source: string = code("DashboardLayout.tsx");
    const outlineProps: readonly string[] = [...source.matchAll(/variant="outline"/gu)].map(
      ([match]: RegExpExecArray): string => match,
    );

    // Two controls, two call sites. Rendering one through the catalog and leaving
    // the other hand-rolled is exactly the drift Wallow-lrlm.5.4 collapsed.
    expect(outlineProps, "both NavToggle and MobileMenuButton are outline Buttons").toHaveLength(2);
  });
});
