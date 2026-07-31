import { describe, expect, it } from "vitest";

import { buttonRecipe } from "./components/button/button.styles";
import {
  errorBannerRecipe,
  errorBannerTextRecipe,
} from "./components/error-banner/error-banner.styles";
import {
  navigationMenuLinkRecipe,
  navigationMenuTriggerRecipe,
} from "./components/navigation-menu/navigation-menu.styles";
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

/**
 * The colour DIMENSION a utility occupies — its variant prefix plus the property
 * it paints (`hover:|bg`, `|text`, `data-[popup-open]:|text`) — or `null` when
 * the class paints no colour.
 *
 * This is the unit the `sidebar` arm has to match the page arm in (`hover|bg`,
 * `data-[popup-open]|text`, …). Naming the right tokens is not enough: `twMerge`
 * only drops the class a caller CONFLICTS with, so a dimension the sidebar arm
 * never names is a dimension the page arm still owns on the inverted rail.
 */
function colorDimensionOf(cls: string): string | null {
  if (colorTokenOf(cls) === null) {
    return null;
  }

  const segments: readonly string[] = cls.split(":");
  const utility: string = segments.at(-1) ?? "";
  const variant: string = segments.slice(0, -1).join(":");
  const property: string =
    COLOUR_PREFIXES.find((prefix: string): boolean => utility.startsWith(`${prefix}-`)) ?? "";

  return `${variant}|${property}`;
}

/** Every class in `classes` naming a theme colour, page family or sidebar family. */
function colorClasses(classes: string): readonly string[] {
  return [...pageSurfaceColorUtilities(classes), ...sidebarColorUtilities(classes)];
}

/**
 * The theme colours one arm contributes that the `other` arm does not — the arm's
 * OWN paint, with everything inherited from the recipe's base string removed.
 */
function onlyIn(arm: string, other: string): readonly string[] {
  const shared: ReadonlySet<string> = new Set(other.split(/\s+/u));

  return colorClasses(arm).filter((cls: string): boolean => !shared.has(cls));
}

/** Every colour dimension `classes` occupies, deduplicated and ordered. */
function colorDimensions(classes: string): readonly string[] {
  return [
    ...new Set(
      classes
        .split(/\s+/u)
        .filter(Boolean)
        .map((cls: string): string | null => colorDimensionOf(cls))
        .filter((dimension: string | null): dimension is string => dimension !== null),
    ),
  ].toSorted();
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

  it("reads a colour dimension as its variant prefix plus the property it paints", () => {
    // Demonstrated rather than trusted, for the same reason as the detector
    // above: a dimension reader that returned `null` everywhere would make the
    // parity assertion below compare two empty lists and pass forever.
    expect(
      colorDimensions(
        "text-sm rounded-md text-foreground hover:bg-accent hover:text-accent-foreground data-[popup-open]:bg-accent",
      ),
    ).toEqual(["data-[popup-open]|bg", "hover|bg", "hover|text", "|text"]);
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

describe("navigationMenuTriggerRecipe — surface", () => {
  /*
   * The Link's sibling (Wallow-lrlm.10). A trigger is not a button in a toolbar,
   * it is a NAV ROW that happens to open a panel and has to sit flush beside the
   * `Link` rows in the same list — so the two recipes carry the same row shape,
   * and from here the same surface axis, spelled in the same two palettes.
   *
   * NOT a live defect when this was filed: no app renders a trigger, so nothing
   * on a rail paints wrong today. It is closed anyway because a trigger dropped
   * into a sidebar reproduces exactly the defect the Link's axis just fixed, and
   * because a reader who has seen the Link recipe will assume this one matches.
   *
   * The class-list half lives here for the reason this whole file exists (see the
   * header): "the row's colour no longer DEPENDS on a consumer suppressing
   * catalog classes by name" is a statement about the recipe's output BEFORE any
   * consumer `className` reaches it, and no rendered pixel can express it. What
   * the row actually PAINTS on a real rail is measured in the storybook project —
   * navigation-menu.stories.tsx's TriggerSidebarSurface.
   */
  it("keeps the page palette on the page arm", () => {
    // The default is what every existing consumer renders, and it must go on
    // painting exactly as it does today.
    const page: string = cn(navigationMenuTriggerRecipe());

    expect(pageSurfaceColorUtilities(page)).toEqual(
      expect.arrayContaining([
        "hover:bg-accent",
        "hover:text-accent-foreground",
        "data-[popup-open]:bg-accent",
        "data-[popup-open]:text-accent-foreground",
      ]),
    );
    expect(cn(navigationMenuTriggerRecipe({ surface: "page" }))).toBe(page);
  });

  it("hands the sidebar arm over with no page-surface colour in it", () => {
    // THE criterion: whatever a trigger on the rail ends up wearing, none of it
    // may be a page colour the consumer then has to out-merge by name.
    const sidebar: string = cn(navigationMenuTriggerRecipe({ surface: "sidebar" }));

    expect(pageSurfaceColorUtilities(sidebar)).toEqual([]);
  });

  it("names, in the sidebar arm ALONE, every colour dimension the trigger renders", () => {
    // RULE 2, and the half a token-by-token assertion misses. `twMerge` drops
    // only the class a caller conflicts with, so a dimension the sidebar arm
    // leaves unnamed — the open row's text, say — keeps whatever the page arm
    // or the surrounding page put there.
    //
    // It is asserted against the arm's OWN contribution rather than against the
    // whole merged string on purpose: comparing the two merged strings passes
    // trivially while both arms are empty and every colour still sits in the
    // base, which is precisely the state this bead exists to leave.
    const page: readonly string[] = colorClasses(navigationMenuTriggerRecipe({ surface: "page" }));
    const sidebarOwn: readonly string[] = onlyIn(
      navigationMenuTriggerRecipe({ surface: "sidebar" }),
      navigationMenuTriggerRecipe({ surface: "page" }),
    );

    expect(colorDimensions(sidebarOwn.join(" "))).toEqual(colorDimensions(page.join(" ")));
  });

  it("leaves no colour in the base string for an arm to have to take back", () => {
    // RULE 1's observable consequence. The axis is declared LAST so its
    // utilities land after everything else and `cn()` collapses each pair in its
    // favour — but the durable form of that is simply that no theme colour
    // survives BOTH arms, i.e. every colour a trigger renders came from exactly
    // one arm. A colour left in the base is one an arm can only out-merge, and
    // out-merging is the race this axis exists to end.
    const page: readonly string[] = cn(navigationMenuTriggerRecipe({ surface: "page" })).split(
      /\s+/u,
    );
    const sidebar: readonly string[] = cn(
      navigationMenuTriggerRecipe({ surface: "sidebar" }),
    ).split(/\s+/u);
    const shared: string = page.filter((cls: string): boolean => sidebar.includes(cls)).join(" ");

    expect([...pageSurfaceColorUtilities(shared), ...sidebarColorUtilities(shared)]).toEqual([]);
  });

  it("paints the sidebar arm's own rest, hover and open colours, rather than none", () => {
    // Without this, "no page colour" is satisfied by emitting no colour at all —
    // which puts the row's paint straight back in the consumer's hands and
    // leaves a hovered or open row with no feedback.
    const sidebarColours: readonly string[] = sidebarColorUtilities(
      cn(navigationMenuTriggerRecipe({ surface: "sidebar" })),
    );

    expect(
      sidebarColours.filter((cls: string): boolean => !cls.includes(":")),
      "the row has no rest colour of its own",
    ).not.toEqual([]);
    expect(
      sidebarColours.filter((cls: string): boolean => cls.startsWith("hover:")),
      "the row has no hover colour of its own",
    ).not.toEqual([]);
    expect(
      sidebarColours.filter((cls: string): boolean => cls.startsWith("data-[popup-open]:")),
      "the row has no open colour of its own",
    ).not.toEqual([]);
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
