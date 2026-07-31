import { render } from "@bc-solutions-coder/testing/render";
import type { ComponentProps, ReactElement } from "react";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";

import { cn } from "../../core/cn";
import { Button } from "./button";
import { buttonRecipe, type ButtonRecipeProps } from "./button.styles";

/*
 * EXEMPLAR SPEC (Wallow-m5aq.2.1), extended by Wallow-lrlm.3.1 (the upgraded
 * recipe: outline/ghost/link variants, sm/md/lg/icon sizes, width and shape
 * groups, hover + focus-visible treatment). Every component task copies the
 * shape of this file, so the shape is part of the deliverable:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. `*.test.tsx` under src/ is collected there automatically; no
 *      environment pragma, and never jsdom/happy-dom (.claude/rules/TESTING.md).
 *   2. NOTHING is mocked. packages/ui specs render the actual Base UI part and
 *      read the actual rendered attributes.
 *   3. The COMPONENT is what proves the recipe reaches the DOM: the forwarding
 *      test below renders every variant group through `<Button>` and compares
 *      against the recipe, so a group the component forgets to pass on fails
 *      here even though the recipe itself is correct.
 *   4. Class assertions are an ORDER-FREE SET, because `cn()`/tailwind-merge is
 *      free to reorder. Where the recipe is read directly it is read THROUGH
 *      `cn()` too, so the comparison sees the same merge the component renders.
 *   5. The recipe's own content is asserted as REQUIRED UTILITIES plus
 *      DISTINCTNESS, not as one exact string per combination: 6 x 4 x 2 x 2 = 96
 *      combinations, and pinning each one would encode the implementer's padding
 *      scale rather than the contract. What IS pinned exactly: the legacy
 *      recipe's utilities (below), the two shape radii, and `w-full` belonging to
 *      `width` rather than to the base string.
 *
 * COMPAT GUARANTEE (this component only): the pre-rebuild Button was a
 * hand-rolled string-append with a measured recipe used 11x across wallow-auth.
 * Its class set must survive, with ONE deliberate delta pinned by its own test:
 * `disabled:opacity-50` becomes `data-[disabled]:opacity-50`, because Base UI
 * drives state through data-attributes and the `:disabled` pseudo-class does not
 * exist on a `render`-prop anchor. The new variant groups keep that guarantee by
 * DEFAULTING to today's rendering: `size="md"` carries the legacy padding and
 * type scale, `width="full"` carries the legacy `w-full`, `shape="rounded"`
 * carries the legacy `rounded-md`.
 */

type ButtonVariantName = NonNullable<ButtonRecipeProps["variant"]>;
type ButtonSizeName = NonNullable<ButtonRecipeProps["size"]>;
type ButtonWidthName = NonNullable<ButtonRecipeProps["width"]>;
type ButtonShapeName = NonNullable<ButtonRecipeProps["shape"]>;

const VARIANTS: ButtonVariantName[] = [
  "primary",
  "secondary",
  "destructive",
  "outline",
  "ghost",
  "link",
];
const SIZES: ButtonSizeName[] = ["sm", "md", "lg", "icon"];
const WIDTHS: ButtonWidthName[] = ["auto", "full"];
const SHAPES: ButtonShapeName[] = ["rounded", "pill"];

/** The three pre-existing variants and the token pair each one must keep. */
const SOLID_VARIANT_TOKENS: [ButtonVariantName, string, string][] = [
  ["primary", "bg-primary", "text-primary-foreground"],
  ["secondary", "bg-secondary", "text-secondary-foreground"],
  ["destructive", "bg-destructive", "text-destructive-foreground"],
];

/** The pre-rebuild recipe, verbatim from the Button this replaces. */
const LEGACY_PRIMARY_RECIPE =
  "w-full rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50";

/** The one legacy utility the rebuild is allowed to drop, and its replacement. */
const LEGACY_DISABLED_UTILITY = "disabled:opacity-50";
const STATE_DISABLED_UTILITY = "data-[disabled]:opacity-50";

/**
 * Every colour name `@bc-solutions-coder/styles` defines in its `@theme` block.
 * The recipe may reference these and nothing else — a `bg-*` outside this list is
 * either a raw Tailwind palette colour or a token that does not exist.
 */
const SEMANTIC_COLOR_TOKENS = new Set([
  "transparent",
  "current",
  "inherit",
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
  "destructive",
  "destructive-foreground",
  "border",
  "input",
  "ring",
  "sidebar",
  "sidebar-foreground",
  "sidebar-accent",
  "success",
  "success-foreground",
]);

/** Tailwind's built-in palette — the `bg-blue-500` family the criteria forbid. */
const RAW_PALETTE_COLOR =
  /-(?:slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)-\d{2,3}$/u;

/** A literal colour smuggled in as an arbitrary value: `bg-[#fff]`, `text-[rgb(…)]`. */
const ARBITRARY_COLOR = /\[(?:#|rgb|hsl|oklch|color\()/u;

/** `bg-black` / `text-white` — raw, not a token, and unthemeable. */
const ABSOLUTE_COLOR = /^(?:bg|text|border|ring|outline|decoration)-(?:black|white)$/u;

/** The focus indicator's colour, however the implementer draws the indicator. */
const FOCUS_RING_TOKEN = /^(?:ring|outline)-ring(?:\/\d+)?$/u;

/** The class minus its modifiers: `hover:bg-accent` -> `bg-accent`. */
function baseUtility(className: string): string {
  const separator = className.lastIndexOf(":");
  return separator === -1 ? className : className.slice(separator + 1);
}

/** The class's modifier prefixes: `focus-visible:ring-2` -> `["focus-visible"]`. */
function modifiersOf(className: string): string[] {
  const separator = className.lastIndexOf(":");
  return separator === -1 ? [] : className.slice(0, separator).split(":");
}

/** True when the class applies in the button's resting state, not on a state. */
function isRestingState(className: string): boolean {
  return modifiersOf(className).length === 0;
}

/** A `bg-*` / `text-*` colour value with any opacity suffix removed. */
function tokenOf(utility: string, prefix: string): string {
  return utility.slice(prefix.length + 1).split("/")[0];
}

/**
 * The classes a props combination lands on the DOM, order-free. Read through
 * `cn()` so tailwind-merge collapses the same conflicts the component's own
 * `cn(buttonRecipe(...), className)` collapses — `text-sm` in the base against a
 * size's own type scale, for one.
 */
function recipeClasses(props: ButtonRecipeProps = {}): string[] {
  return cn(buttonRecipe(props)).split(" ").filter(Boolean).toSorted();
}

/** Every combination the recipe can produce — 6 x 4 x 2 x 2. */
function allCombinations(): ButtonRecipeProps[] {
  return VARIANTS.flatMap((variant) =>
    SIZES.flatMap((size) =>
      WIDTHS.flatMap((width) => SHAPES.map((shape) => ({ variant, size, width, shape }))),
    ),
  );
}

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function onlyButton(container: HTMLElement): HTMLButtonElement {
  const button = container.querySelector("button");
  expect(button).not.toBeNull();
  return button as HTMLButtonElement;
}

/** The classes `<Button>` actually renders for a props combination. */
async function renderedClasses(element: ReactElement): Promise<string[]> {
  const { container } = await render(element);
  return classSet(onlyButton(container));
}

describe("Button recipe options", () => {
  // The four `Record<…, true>` literals are compile-time exhaustiveness checks:
  // a missing key or a key the recipe does not declare fails `pnpm typecheck`,
  // which is the half a runtime assertion cannot cover.

  it("offers exactly six variants", () => {
    const declared: Record<ButtonVariantName, true> = {
      primary: true,
      secondary: true,
      destructive: true,
      outline: true,
      ghost: true,
      link: true,
    };

    expect(Object.keys(declared).toSorted()).toEqual([...VARIANTS].toSorted());
  });

  it("offers exactly four sizes", () => {
    const declared: Record<ButtonSizeName, true> = { sm: true, md: true, lg: true, icon: true };

    expect(Object.keys(declared).toSorted()).toEqual([...SIZES].toSorted());
  });

  it("offers an auto and a full width", () => {
    const declared: Record<ButtonWidthName, true> = { auto: true, full: true };

    expect(Object.keys(declared).toSorted()).toEqual([...WIDTHS].toSorted());
  });

  it("offers a rounded and a pill shape", () => {
    const declared: Record<ButtonShapeName, true> = { rounded: true, pill: true };

    expect(Object.keys(declared).toSorted()).toEqual([...SHAPES].toSorted());
  });

  it("defaults to the primary variant at the md size, full width, rounded shape", () => {
    // The compat default: today's Button is a full-width rounded-md primary, and
    // every one of the 11 existing call sites passes no size, width or shape.
    expect(recipeClasses()).toEqual(
      recipeClasses({ variant: "primary", size: "md", width: "full", shape: "rounded" }),
    );
  });

  it("gives every variant a distinct class set", () => {
    const rendered = VARIANTS.map((variant) => recipeClasses({ variant }).join(" "));

    expect(new Set(rendered).size).toBe(VARIANTS.length);
  });

  it("gives every size a distinct class set", () => {
    const rendered = SIZES.map((size) => recipeClasses({ size }).join(" "));

    expect(new Set(rendered).size).toBe(SIZES.length);
  });
});

describe("Button variant treatment", () => {
  it.each(SOLID_VARIANT_TOKENS)(
    "keeps the %s variant on its %s / %s token pair",
    (variant, surface, foreground) => {
      const classes = recipeClasses({ variant });

      expect(classes).toContain(surface);
      expect(classes).toContain(foreground);
    },
  );

  it("keeps each solid variant's surface token off the other solid variants", () => {
    for (const [variant, surface] of SOLID_VARIANT_TOKENS) {
      for (const [other] of SOLID_VARIANT_TOKENS) {
        if (other !== variant) {
          expect(recipeClasses({ variant: other }), `${other} leaked ${surface}`).not.toContain(
            surface,
          );
        }
      }
    }
  });

  it("draws the outline variant as a border with no solid surface", () => {
    // What makes `outline` a distinct name rather than a synonym for `ghost`.
    const classes = recipeClasses({ variant: "outline" });
    const resting = classes.filter((name) => isRestingState(name));

    expect(resting.some((name) => name === "border" || /^border-\d/u.test(name))).toBe(true);
    expect(resting.some((name) => /^border-[a-z]/u.test(name))).toBe(true);
    expect(resting.filter((name) => name.startsWith("bg-") && name !== "bg-transparent")).toEqual(
      [],
    );
  });

  it("draws the ghost variant with neither surface nor border until hover", () => {
    const classes = recipeClasses({ variant: "ghost" });
    const resting = classes.filter((name) => isRestingState(name));

    expect(resting.filter((name) => name.startsWith("bg-") && name !== "bg-transparent")).toEqual(
      [],
    );
    expect(resting.filter((name) => name === "border" || /^border-[a-z]/u.test(name))).toEqual([]);
    expect(classes.some((name) => name.startsWith("hover:bg-"))).toBe(true);
  });

  it("draws the link variant as underlined text with no box at all", () => {
    const classes = recipeClasses({ variant: "link" });
    const resting = classes.filter((name) => isRestingState(name));

    expect(classes.some((name) => baseUtility(name) === "underline")).toBe(true);
    expect(resting.filter((name) => name.startsWith("bg-") && name !== "bg-transparent")).toEqual(
      [],
    );
    expect(resting.filter((name) => name === "border" || /^border-[a-z]/u.test(name))).toEqual([]);
  });

  it.each(VARIANTS)("gives the %s variant a hover treatment", (variant) => {
    expect(recipeClasses({ variant }).filter((name) => name.startsWith("hover:"))).not.toEqual([]);
  });

  it("gives the solid variants hover treatments that differ from one another", () => {
    // One shared `hover:bg-accent` across primary/secondary/destructive would
    // make a destructive button hover into a neutral surface.
    const hovers = SOLID_VARIANT_TOKENS.map(([variant]) =>
      recipeClasses({ variant })
        .filter((name) => name.startsWith("hover:"))
        .join(" "),
    );

    expect(new Set(hovers).size).toBe(SOLID_VARIANT_TOKENS.length);
  });

  it.each(VARIANTS)("gives the %s variant a focus-visible ring on the ring token", (variant) => {
    // Keyboard focus is the one state a story cannot show and a hover cannot
    // stand in for; the catalog draws it as `focus-visible:ring-2
    // focus-visible:ring-ring` (see toolbar/menubar). The colour is pinned, the
    // width and offset are the implementer's.
    const focus = recipeClasses({ variant }).filter((name) => name.startsWith("focus-visible:"));

    expect(focus).not.toEqual([]);
    expect(focus.some((name) => FOCUS_RING_TOKEN.test(baseUtility(name)))).toBe(true);
  });

  it.each(VARIANTS)("keeps the data-driven disabled treatment on the %s variant", (variant) => {
    // Base UI's state contract, and the delta the compat guarantee allows: the
    // treatment must survive on EVERY variant, not just the default one.
    expect(recipeClasses({ variant })).toContain(STATE_DISABLED_UTILITY);
  });
});

describe("Button size scale", () => {
  it("keeps the legacy padding and type scale on the md size", () => {
    const classes = recipeClasses({ size: "md" });

    expect(classes).toContain("px-3");
    expect(classes).toContain("py-2");
    expect(classes).toContain("text-sm");
  });

  it.each(["sm", "lg"] as const)("gives the %s size its own padding and type scale", (size) => {
    const classes = recipeClasses({ size });
    const md = recipeClasses({ size: "md" });

    expect(classes.filter((name) => /^p[xy]?-/u.test(name))).not.toEqual(
      md.filter((name) => /^p[xy]?-/u.test(name)),
    );
    expect(classes.some((name) => /^text-(?:xs|sm|base|lg)$/u.test(name))).toBe(true);
  });

  it("makes the icon size square", () => {
    // An icon-only button is a square target, not a text button with a glyph in
    // it: either a single `size-*` utility or a matching h/w pair.
    const classes = recipeClasses({ size: "icon" });
    const square = classes.find((name) => name.startsWith("size-"));
    const height = classes.find((name) => name.startsWith("h-"));
    const width = classes.find((name) => /^w-\d/u.test(name));

    expect(
      square !== undefined ||
        (height !== undefined && width !== undefined && height.slice(2) === width.slice(2)),
      `icon size is not square: ${classes.join(" ")}`,
    ).toBe(true);
  });

  it("gives the icon size no horizontal text padding", () => {
    // A square target sized by `size-*` must not also carry the text sizes'
    // `px-3`, or the glyph sits off-centre in the box.
    expect(
      recipeClasses({ size: "icon" }).filter((name) => name.startsWith("px-") && name !== "px-0"),
    ).toEqual([]);
  });
});

describe("Button width and shape", () => {
  it("puts w-full on the full width rather than in the base string", () => {
    // The base string carried `w-full` before this bead, which made every
    // non-full button an override fighting the base class.
    expect(recipeClasses({ width: "full" })).toContain("w-full");
    expect(recipeClasses({ width: "auto" })).not.toContain("w-full");
  });

  it("keeps the width group independent of the variant", () => {
    for (const variant of VARIANTS) {
      expect(recipeClasses({ variant, width: "auto" }), variant).not.toContain("w-full");
      expect(recipeClasses({ variant, width: "full" }), variant).toContain("w-full");
    }
  });

  it("keeps the legacy rounded-md on the rounded shape and rounds the pill fully", () => {
    expect(recipeClasses({ shape: "rounded" })).toContain("rounded-md");
    expect(recipeClasses({ shape: "pill" })).toContain("rounded-full");
  });

  it("renders exactly one radius utility per shape", () => {
    // A `rounded-md` left in the base string would survive alongside
    // `rounded-full` only if tailwind-merge failed to collapse them; asserting
    // the count keeps the radius decision in one place.
    for (const shape of SHAPES) {
      expect(
        recipeClasses({ shape }).filter((name) => /^rounded(?:-|$)/u.test(name)),
        shape,
      ).toHaveLength(1);
    }
  });
});

describe("Button token discipline", () => {
  it("references no raw Tailwind colour in any combination", () => {
    const offenders: string[] = [];

    for (const props of allCombinations()) {
      for (const className of recipeClasses(props)) {
        const utility = baseUtility(className);
        if (
          RAW_PALETTE_COLOR.test(utility) ||
          ARBITRARY_COLOR.test(utility) ||
          ABSOLUTE_COLOR.test(utility)
        ) {
          offenders.push(`${props.variant}/${props.size}: ${className}`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it("draws every surface and border from a semantic token", () => {
    // `bg-*` and `border-<name>` are unambiguously colour in Tailwind, so they
    // can be checked against the token list rather than only against a denylist.
    const offenders: string[] = [];

    for (const props of allCombinations()) {
      for (const className of recipeClasses(props)) {
        const utility = baseUtility(className);
        if (utility.startsWith("bg-") && !SEMANTIC_COLOR_TOKENS.has(tokenOf(utility, "bg"))) {
          offenders.push(`${props.variant}/${props.size}: ${className}`);
        }
        if (
          /^border-[a-z]/u.test(utility) &&
          !SEMANTIC_COLOR_TOKENS.has(tokenOf(utility, "border"))
        ) {
          offenders.push(`${props.variant}/${props.size}: ${className}`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it("honours prefers-reduced-motion wherever it animates", () => {
    // Either every transition is `motion-safe:`-gated, or the recipe switches it
    // off under `motion-reduce:`. A bare `transition-colors` is neither.
    for (const props of allCombinations()) {
      const classes = recipeClasses(props);
      const animated = classes.filter((name) =>
        /^(?:transition|duration|animate)(?:-|$)/u.test(baseUtility(name)),
      );

      if (animated.length > 0) {
        const allMotionSafe = animated.every((name) => modifiersOf(name).includes("motion-safe"));
        const disabledUnderReducedMotion = classes.includes("motion-reduce:transition-none");

        expect(
          allMotionSafe || disabledUnderReducedMotion,
          `${props.variant}/${props.size} animates without a reduced-motion escape: ${animated.join(" ")}`,
        ).toBe(true);
      }
    }
  });
});

describe("Button component", () => {
  it("renders the primary recipe by default", async () => {
    const { container } = await render(<Button>Sign in</Button>);

    const button = onlyButton(container);
    expect(classSet(button)).toEqual(recipeClasses());
    expect(button.textContent).toBe("Sign in");
  });

  it.each(VARIANTS)("forwards the %s variant to the recipe", async (variant) => {
    expect(await renderedClasses(<Button variant={variant}>Continue</Button>)).toEqual(
      recipeClasses({ variant }),
    );
  });

  it.each(SIZES)("forwards the %s size to the recipe", async (size) => {
    // The prop-forwarding half: `size` lands in `...rest` and becomes a DOM
    // attribute unless button.tsx destructures it into the recipe call.
    expect(await renderedClasses(<Button size={size}>Continue</Button>)).toEqual(
      recipeClasses({ size }),
    );
  });

  it.each(WIDTHS)("forwards the %s width to the recipe", async (width) => {
    expect(await renderedClasses(<Button width={width}>Continue</Button>)).toEqual(
      recipeClasses({ width }),
    );
  });

  it.each(SHAPES)("forwards the %s shape to the recipe", async (shape) => {
    expect(await renderedClasses(<Button shape={shape}>Continue</Button>)).toEqual(
      recipeClasses({ shape }),
    );
  });

  it("forwards every variant group at once", async () => {
    expect(
      await renderedClasses(
        <Button variant="outline" size="lg" width="auto" shape="pill">
          Continue
        </Button>,
      ),
    ).toEqual(recipeClasses({ variant: "outline", size: "lg", width: "auto", shape: "pill" }));
  });

  it("leaks no recipe prop onto the rendered element as an attribute", async () => {
    // `size` and `width` are real HTML attribute names; if button.tsx spreads
    // them instead of consuming them they land in the markup.
    const { container } = await render(
      <Button size="icon" width="auto" shape="pill">
        Continue
      </Button>,
    );
    const button = onlyButton(container);

    for (const attribute of ["variant", "size", "width", "shape"]) {
      expect(button.hasAttribute(attribute), attribute).toBe(false);
    }
  });

  it("keeps every class of the pre-rebuild recipe except the disabled-state swap", async () => {
    // The compat guarantee, stated as its own test so a future change to the
    // recipe has to acknowledge which of the 11 measured call sites it moves.
    // The new variant groups are additive: their defaults reproduce this set.
    const { container } = await render(<Button>Sign in</Button>);

    const button = onlyButton(container);
    for (const legacy of LEGACY_PRIMARY_RECIPE.split(" ")) {
      if (legacy !== LEGACY_DISABLED_UTILITY) {
        expect(button.classList.contains(legacy), legacy).toBe(true);
      }
    }

    expect(button.classList.contains(LEGACY_DISABLED_UTILITY)).toBe(false);
    expect(button.classList.contains(STATE_DISABLED_UTILITY)).toBe(true);
  });

  it("forwards native button attributes (type, disabled)", async () => {
    const { container } = await render(
      <Button type="submit" disabled>
        Submit
      </Button>,
    );

    const button = onlyButton(container);
    expect(button.type).toBe("submit");
    expect(button.disabled).toBe(true);
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Button data-testid="login-submit">Sign in</Button>);

    expect(container.querySelector('[data-testid="login-submit"]')).not.toBeNull();
  });

  it("exposes the disabled state as a data attribute", async () => {
    // Base UI's state contract, and what `data-[disabled]:opacity-50` hooks off.
    // A hand-rolled <button disabled> renders no such attribute.
    const { container } = await render(<Button disabled>Submit</Button>);

    expect(onlyButton(container).getAttribute("data-disabled")).toBe("");
  });

  it("carries no disabled data attribute when enabled", async () => {
    const { container } = await render(<Button>Submit</Button>);

    expect(onlyButton(container).hasAttribute("data-disabled")).toBe(false);
  });

  it("lets a caller className override a recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both `bg-primary` and `bg-accent` on
    // the element and fails here.
    const { container } = await render(<Button className="bg-accent">Sign in</Button>);

    const button = onlyButton(container);
    expect(button.classList.contains("bg-accent")).toBe(true);
    expect(button.classList.contains("bg-primary")).toBe(false);
    expect(button.classList.contains("rounded-md")).toBe(true);
    expect(button.classList.contains("text-primary-foreground")).toBe(true);
  });

  it("composes onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes. `nativeButton={false}` tells Base UI the rendered element is
    // not a <button>, which is what keeps it from logging a dev-mode error.
    const { container } = await render(
      <Button render={<a href="/docs" />} nativeButton={false} variant="link" width="auto">
        Read the docs
      </Button>,
    );

    const anchor = container.querySelector("a");
    expect(anchor).not.toBeNull();
    expect(anchor?.getAttribute("href")).toBe("/docs");
    expect(classSet(anchor as Element)).toEqual(recipeClasses({ variant: "link", width: "auto" }));
    expect(container.querySelector("button")).toBeNull();
  });

  it("invokes the caller's onClick", async () => {
    // Base UI merges its own handlers over the caller's; this pins that the
    // caller's handler still runs.
    const onClick = vi.fn();
    const { container } = await render(<Button onClick={onClick}>Sign in</Button>);

    await userEvent.click(onlyButton(container));

    expect(onClick).toHaveBeenCalledTimes(1);
  });
});

/**
 * A stand-in for TanStack Router's `Link`: a COMPONENT-typed render element that
 * turns its own routing prop into the `href`. The distinction from a literal
 * `<a href>` is the whole difficulty of the spec below it — `render.type` here is
 * a function and `render.props` carries no `href`, so nothing about this element
 * says "anchor" until it has actually mounted. `apps/wallow-web` composes the
 * catalog Button exactly this way for its header CTA.
 */
function StandInRouterLink({ to, ...rest }: { to: string } & ComponentProps<"a">): ReactElement {
  return <a href={to} {...rest} />;
}

/**
 * ROLE SEMANTICS (Wallow-lrlm.12).
 *
 * Base UI's `useButton` merges `isNativeButton ? { type: "button" } : { role:
 * "button" }` into the props of whatever `render` substitutes, so every
 * anchor-composed catalog Button announces a NAVIGATION as an ACTION: it is
 * dropped from a screen reader's links list, while the retained `href` still
 * gives the browser open-in-new-tab, copy-link-address and a status-bar target.
 * That contradiction is the WCAG 2.2 SC 4.1.2 Name/Role/Value failure this block
 * exists to make loud.
 *
 * WHY THIS BLOCK IS A `*.test.tsx` AND NOT A STORY: the catalog's render coverage
 * lives in `button.stories.tsx`, but the subject here is the COMPUTED ACCESSIBLE
 * ROLE, which a story renders and cannot assert. This is the same class of edge
 * as the data-attribute and className-override specs above it.
 *
 * WHY THE ASSERTIONS READ THE ROLE ENGINE AND NOT `getAttribute("role")`: the
 * defect is what the accessibility tree reports, and an implementation may reach
 * the right answer either by setting `role="link"` or by omitting the attribute
 * so the anchor's IMPLICIT role applies. Both are correct; pinning the literal
 * attribute would pick one and reject the other. `getByRole` resolves implicit
 * and explicit roles the way a browser does, so it accepts either and still
 * fails on `role="button"`. `.query()` is used rather than `expect.element` so a
 * negative case resolves immediately instead of retrying to timeout.
 *
 * WHY BASE UI IS NOT ASKED TO FIX THIS: it declines to. Base UI 1.6.0 documents
 * the role as INTENTIONAL ("The Button component enforces button semantics
 * (`role="button"`, keyboard interaction, disabled state). It should not be used
 * for links") and exposes no opt-out prop — `nativeButton` only chooses between
 * `type="button"` and `role="button"`. The override is the documented merge
 * ORDER instead: `getButtonProps` merges `otherExternalProps` LAST, so a `role`
 * the catalog Button supplies wins. Base UI's keyboard handling already agrees
 * with these specs — `isValidLinkElement` (tagName `A` + `href`) suppresses the
 * Space/Enter synthesis for exactly these elements. Only the role disagrees.
 */
describe("Button link semantics", () => {
  /** The composed element, whatever tag `render` substituted. */
  function composed(container: HTMLElement): Element {
    const element = container.firstElementChild;
    expect(element).not.toBeNull();
    return element as Element;
  }

  it("announces an anchor composed through render as a link", async () => {
    // THE DEFECT. `<a href>` is a link in every sense the browser cares about,
    // and the catalog must say so without the caller asking.
    const screen = await render(
      <Button render={<a href="/docs" />} nativeButton={false} variant="link" width="auto">
        Read the docs
      </Button>,
    );

    expect(screen.getByRole("link", { name: "Read the docs" }).query()).toBe(
      composed(screen.container),
    );
    expect(screen.getByRole("button", { name: "Read the docs" }).query()).toBeNull();
  });

  it("announces a component-typed render that resolves to an anchor as a link", async () => {
    // The wallow-web shape. The element only becomes an anchor once the
    // component has rendered, so an implementation that inspects `render.type`
    // or `render.props.href` passes the spec above and fails here. Both call
    // shapes are shipped today, so both are the contract.
    const screen = await render(
      <Button render={<StandInRouterLink to="/dashboard/apps/register" />} nativeButton={false}>
        Register New App
      </Button>,
    );

    const element = composed(screen.container);
    expect(element.tagName).toBe("A");
    expect(element.getAttribute("href")).toBe("/dashboard/apps/register");
    expect(screen.getByRole("link", { name: "Register New App" }).query()).toBe(element);
    expect(screen.getByRole("button", { name: "Register New App" }).query()).toBeNull();
  });

  it("keeps button semantics for a non-anchor composed through render", async () => {
    // The other arm, and the reason the fix cannot simply drop the role: a <div>
    // has no implicit role at all, so without Base UI's `role="button"` this
    // control would announce as nothing. Passes today; it fails the moment a fix
    // strips the role unconditionally instead of only for links.
    const screen = await render(
      <Button render={<div />} nativeButton={false}>
        Complex content
      </Button>,
    );

    expect(screen.getByRole("button", { name: "Complex content" }).query()).toBe(
      composed(screen.container),
    );
  });

  it("keeps button semantics for an anchor with no href", async () => {
    // An <a> without an href is NOT a link — it has no implicit role, and Base
    // UI's own `isValidLinkElement` requires the href before it treats the
    // element as one. A fix that sniffs the tag name alone announces this
    // control as a link to nowhere and fails here.
    const screen = await render(
      <Button render={<a />} nativeButton={false}>
        Not a destination
      </Button>,
    );

    expect(screen.getByRole("link", { name: "Not a destination" }).query()).toBeNull();
    expect(screen.getByRole("button", { name: "Not a destination" }).query()).toBe(
      composed(screen.container),
    );
  });

  it("lets a caller's explicit role win over the catalog default", async () => {
    // The escape hatch has to survive the fix. A composed Button inside a menu
    // or a toolbar still needs its own role, so the catalog's default must be a
    // DEFAULT and not an override — the same merge order the fix itself relies
    // on, asserted from the caller's side.
    const { container } = await render(
      <Button render={<a href="/docs" role="menuitem" />} nativeButton={false}>
        Read the docs
      </Button>,
    );

    expect(composed(container).getAttribute("role")).toBe("menuitem");
  });

  it("leaves a native button carrying no role attribute", async () => {
    // The default path is untouched: a real <button> gets `type="button"` and no
    // role, and its implicit button role is what the tree reports. Guards
    // against a fix that reaches the uncomposed component at all.
    const screen = await render(<Button>Sign in</Button>);

    const button = onlyButton(screen.container);
    expect(button.hasAttribute("role")).toBe(false);
    expect(button.getAttribute("type")).toBe("button");
    expect(screen.getByRole("button", { name: "Sign in" }).query()).toBe(button);
  });
});
