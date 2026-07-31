import { describe, expect, it } from "vitest";

import { buttonRecipe } from "./components/button/button.styles";
import {
  errorBannerRecipe,
  errorBannerTextRecipe,
} from "./components/error-banner/error-banner.styles";
import { navigationMenuLinkRecipe } from "./components/navigation-menu/navigation-menu.styles";
import { cn } from "./core/cn";

/**
 * The `surface` axis, across every recipe that has one (Wallow-lrlm.6.4).
 *
 * THE DEFECT THIS AXIS EXISTS FOR. Three catalog components render inside
 * wallow-web's dashboard rail — `ThemeToggle`, `NavigationMenu.Link`,
 * `ErrorBanner` — and every one of them paints from the PAGE palette, because
 * no recipe knew it could be composed onto an inverted surface. The consumer's
 * only recourse was to out-merge the recipe class by class through
 * `tailwind-merge`, and that is not a mechanism, it is a race: `twMerge` drops a
 * recipe class only when the caller supplies one that conflicts AT THE SAME
 * VARIANT, so an unmodified `text-sidebar-foreground` left the link recipe's
 * `hover:text-accent-foreground` standing and a hovered nav label went to 1.27:1
 * while the whole suite stayed green (Wallow-lrlm.5.4).
 *
 * WHY THIS FILE ASSERTS CLASS STRINGS WHEN THE REST OF THE EPIC DOES NOT. A
 * class string is blind to what a component PAINTS, which is why the legibility
 * half of this bead is measured in Chromium against the real fork theme
 * (`apps/wallow-web/src/shared/components/DashboardNav.sidebar-surface.test.tsx`
 * reads the rendered colours). But "the row's colour no longer DEPENDS on the
 * consumer suppressing catalog classes by name" is a statement about the
 * recipe's own output BEFORE any consumer className reaches it — no rendered
 * pixel can express it, because a consumer that still suppresses correctly
 * paints identically to one that does not. So the two halves are split: the
 * catalog proves it hands over a clean class list, the app proves the result is
 * legible.
 *
 * Every assertion here runs the recipe through `cn()` first, because that is
 * what the components do — the question is always what reaches the DOM, not
 * what cva concatenated on the way.
 */

/**
 * The utility prefixes that name a COLOUR. `text-` is in the list even though it
 * also spells a font size; {@link pageSurfaceColorUtilities} keys off the TOKEN
 * after the prefix, so `text-sm` is not a colour and `text-accent-foreground`
 * is.
 */
const COLOUR_PREFIXES: readonly string[] = [
  "bg",
  "text",
  "border",
  "ring",
  "outline",
  "fill",
  "stroke",
  "decoration",
  "divide",
  "caret",
  "from",
  "via",
  "to",
  "shadow",
];

/**
 * The theme's PAGE-surface colour tokens — the palette a component paints in
 * when it sits on the page background. None of them may reach a row on the
 * inverted rail.
 *
 * `destructive` is deliberately absent: an error banner on the rail is still an
 * error, so it may keep naming that token. Whether it stays legible there is
 * measured in the app, not asserted here.
 */
const PAGE_SURFACE_TOKENS: ReadonlySet<string> = new Set([
  "background",
  "foreground",
  "card",
  "card-foreground",
  "popover",
  "popover-foreground",
  "primary",
  "primary-foreground",
  "secondary",
  "secondary-foreground",
  "muted",
  "muted-foreground",
  "accent",
  "accent-foreground",
]);

/** The inverted surface's own family — what a `sidebar` arm is allowed to name. */
const SIDEBAR_TOKENS: ReadonlySet<string> = new Set([
  "sidebar",
  "sidebar-foreground",
  "sidebar-accent",
]);

/** The colour token a utility class names, or `null` when it names no colour. */
function colorTokenOf(cls: string): string | null {
  // Strip every variant prefix — `hover:`, `md:`, `data-[active]:` — and judge
  // the utility underneath. A page colour hidden behind `data-[active]:` is
  // still a page colour, and that is exactly where the inert pair hides.
  const utility: string = cls.split(":").at(-1) ?? "";

  for (const prefix of COLOUR_PREFIXES) {
    if (utility.startsWith(`${prefix}-`)) {
      // `bg-destructive/10` — the opacity modifier is not part of the token.
      return utility.slice(prefix.length + 1).split("/")[0] ?? null;
    }
  }

  return null;
}

/** Every class in `classes` that paints from the page palette. */
function pageSurfaceColorUtilities(classes: string): readonly string[] {
  return classes
    .split(/\s+/u)
    .filter(Boolean)
    .filter((cls: string): boolean => {
      const token: string | null = colorTokenOf(cls);
      return token !== null && PAGE_SURFACE_TOKENS.has(token);
    });
}

/** Every class in `classes` that paints from the sidebar family. */
function sidebarColorUtilities(classes: string): readonly string[] {
  return classes
    .split(/\s+/u)
    .filter(Boolean)
    .filter((cls: string): boolean => {
      const token: string | null = colorTokenOf(cls);
      return token !== null && SIDEBAR_TOKENS.has(token);
    });
}

describe("the page-surface detector", () => {
  /*
   * The detector is the load-bearing part of every assertion below, so it is
   * demonstrated rather than trusted. A detector that silently matched nothing
   * would turn this whole file into a suite that cannot fail.
   */
  it("flags a page-surface colour behind any variant prefix", () => {
    expect(
      pageSurfaceColorUtilities(
        "text-foreground hover:bg-accent data-[active]:text-accent-foreground md:bg-secondary/80",
      ),
    ).toEqual([
      "text-foreground",
      "hover:bg-accent",
      "data-[active]:text-accent-foreground",
      "md:bg-secondary/80",
    ]);
  });

  it("flags neither a non-colour utility nor a sidebar-family colour", () => {
    // `text-sm` is the trap: it shares `text-`'s prefix and names no colour. And
    // `sidebar-foreground` must not be read as `foreground` with a prefix on it.
    expect(
      pageSurfaceColorUtilities(
        "text-sm rounded-md border-t whitespace-nowrap text-sidebar-foreground hover:bg-sidebar-accent bg-sidebar",
      ),
    ).toEqual([]);
  });
});

describe("navigationMenuLinkRecipe — surface", () => {
  it("keeps the page palette on the page arm", () => {
    // The default is what every existing consumer renders, and it must go on
    // painting exactly as it does today. This also anchors the file: the
    // detector demonstrably finds real page colours in a real recipe.
    const page: string = cn(navigationMenuLinkRecipe());

    expect(pageSurfaceColorUtilities(page)).toEqual(
      expect.arrayContaining([
        "text-foreground",
        "hover:bg-accent",
        "hover:text-accent-foreground",
      ]),
    );
    expect(cn(navigationMenuLinkRecipe({ surface: "page" }))).toBe(page);
  });

  it("hands the sidebar arm over with no page-surface colour in it", () => {
    // THE criterion: whatever the rail's rows end up wearing, none of it may be
    // a page colour the consumer then has to out-merge by name.
    const sidebar: string = cn(navigationMenuLinkRecipe({ surface: "sidebar" }));

    expect(pageSurfaceColorUtilities(sidebar)).toEqual([]);
  });

  it("paints the sidebar arm's own rest and hover colours, rather than none", () => {
    // Without this, "no page colour" is satisfied by emitting no colour at all —
    // which would put the row's paint straight back in the consumer's hands and
    // leave a hovered row with no feedback.
    const sidebar: string = cn(navigationMenuLinkRecipe({ surface: "sidebar" }));
    const sidebarColours: readonly string[] = sidebarColorUtilities(sidebar);

    expect(
      sidebarColours.filter((cls: string): boolean => !cls.includes(":")),
      "the row has no rest colour of its own",
    ).not.toEqual([]);
    expect(
      sidebarColours.filter((cls: string): boolean => cls.startsWith("hover:")),
      "the row has no hover colour of its own",
    ).not.toEqual([]);
  });

  it("leaves no inert page-surface active treatment on the sidebar arm", () => {
    // Base UI sets `data-active` only when its own `active` prop is passed, and
    // wallow-web never passes it (TanStack's `activeProps` is a className merge),
    // so this pair does nothing on the rail today — while remaining a latent
    // light block that lights up the moment anything sets the attribute.
    const sidebar: string = cn(navigationMenuLinkRecipe({ surface: "sidebar" }));

    expect(sidebar).not.toContain("data-[active]:bg-accent");
    expect(sidebar).not.toContain("data-[active]:text-accent-foreground");
  });
});

describe("buttonRecipe — surface", () => {
  it("keeps the secondary chip on the page arm", () => {
    const page: string = cn(buttonRecipe({ variant: "secondary" }));

    expect(page).toContain("bg-secondary");
    expect(page).toContain("text-secondary-foreground");
  });

  it("drops the page chip when the same button is composed onto the rail", () => {
    // `ThemeToggle` hard-codes `variant="secondary"`, so the sidebar arm has to
    // WIN that pair through the merge rather than merely add to it — which is
    // why `surface` is declared last in the recipe and why this asserts the
    // merged string a component would actually render.
    const sidebar: string = cn(buttonRecipe({ variant: "secondary", surface: "sidebar" }));

    expect(pageSurfaceColorUtilities(sidebar)).toEqual([]);
  });

  it("paints the sidebar arm from the rail's own family", () => {
    const sidebar: string = cn(buttonRecipe({ variant: "secondary", surface: "sidebar" }));

    expect(sidebarColorUtilities(sidebar), "the button names no sidebar colour").not.toEqual([]);
  });
});

describe("errorBannerRecipe — surface", () => {
  it("keeps the page banner exactly as it is", () => {
    // Eleven page-side call sites render this banner; the arm they get must not
    // move.
    expect(cn(errorBannerRecipe())).toBe(cn(errorBannerRecipe({ surface: "page" })));
    expect(cn(errorBannerRecipe())).toContain("bg-destructive/10");
    expect(cn(errorBannerTextRecipe())).toContain("text-destructive");
  });

  it("paints something different on the rail", () => {
    // A 10% destructive tint under destructive text reads on the page background
    // and all but vanishes on an L 0.20-0.22 rail. Whether the replacement is
    // legible is MEASURED in wallow-web; all this asserts is that the two arms
    // are not the same banner, so a `surface` that changed nothing cannot pass.
    expect(cn(errorBannerRecipe({ surface: "sidebar" }))).not.toBe(cn(errorBannerRecipe()));
    expect(cn(errorBannerTextRecipe({ surface: "sidebar" }))).not.toBe(cn(errorBannerTextRecipe()));
  });
});
