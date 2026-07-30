import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it } from "vitest";

import { Text, type TextAs } from "./text";
import { textRecipe, type TextRecipeProps } from "./text.styles";

/*
 * SPEC for Wallow-lrlm.2.1 (the Text component).
 *
 * Text's visual half — every variant against every colour, in both schemes —
 * lives in `text.stories.tsx`, which the `storybook` vitest project runs with the
 * real Tailwind pipeline and the fork's real theme attached. This file holds only
 * the edges a story cannot express:
 *
 *   - `as` picks the rendered ELEMENT;
 *   - `as` alone picks a default type scale, and an explicit `variant` beats it;
 *   - a caller `className` beats the recipe's own utilities;
 *   - `weight` beats the variant's own weight;
 *   - the colour map is one semantic token per value with NO `/NN` opacity
 *     suffix anywhere — asserted by reading `textRecipe()` directly, no render;
 *   - `bodySm` + `muted` resolves to exactly MutedText's two classes, the
 *     byte-exact contract Wallow-lrlm.2.2 depends on.
 *
 * Class assertions are order-free sets, per the Button exemplar: `cn()` lets
 * tailwind-merge reorder.
 */

type TextVariant = NonNullable<TextRecipeProps["variant"]>;
type TextColor = NonNullable<TextRecipeProps["color"]>;

/** The element each `as` value must render. */
const AS_VALUES: TextAs[] = [
  "h1",
  "h2",
  "h3",
  "h4",
  "h5",
  "h6",
  "p",
  "span",
  "div",
  "label",
  "legend",
  "code",
];

/**
 * The type scale each `as` value derives when no `variant` is supplied. h5/h6 are
 * deliberately absent: the bead leaves those two to the implementer's judgement,
 * so pinning them here would over-specify the lookup table.
 */
const AS_DEFAULT_VARIANT: [TextAs, TextVariant][] = [
  ["h1", "display"],
  ["h2", "title"],
  ["h3", "heading"],
  ["h4", "subheading"],
  ["p", "body"],
  ["span", "body"],
  ["div", "body"],
  ["label", "body"],
  ["legend", "caption"],
  ["code", "code"],
];

/**
 * The single semantic token each `color` value maps to. One utility per value and
 * never an alpha variant — today's apps write `text-foreground/60` by hand, and
 * making that unreproducible is the point of the component.
 */
const COLOR_TOKENS: [TextColor, string][] = [
  ["default", "text-foreground"],
  ["muted", "text-muted-foreground"],
  ["primary", "text-primary"],
  ["accent", "text-accent-foreground"],
  ["destructive", "text-destructive"],
  ["success", "text-success"],
  ["onSidebar", "text-sidebar-foreground"],
  ["onCard", "text-card-foreground"],
  ["onPrimary", "text-primary-foreground"],
];

const VARIANTS: TextVariant[] = [
  "display",
  "title",
  "heading",
  "subheading",
  "body",
  "bodySm",
  "caption",
  "overline",
  "code",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/** The single rendered element — Text renders exactly one node. */
function only(container: HTMLElement): Element {
  const element = container.firstElementChild;
  expect(element).not.toBeNull();
  return element as Element;
}

/** The classes a given props combination actually lands on the DOM node. */
async function renderedClasses(element: ReactElement): Promise<string[]> {
  const { container } = await render(element);
  return classSet(only(container));
}

describe("Text element selection", () => {
  it.each(AS_VALUES)("renders the %s element", async (as) => {
    const { container } = await render(<Text as={as}>Wallow</Text>);

    expect(only(container).tagName.toLowerCase()).toBe(as);
  });

  it("defaults to a paragraph when no as is supplied", async () => {
    const { container } = await render(<Text>Wallow</Text>);

    expect(only(container).tagName.toLowerCase()).toBe("p");
  });

  it("passes children and an app-owned data-testid through to the element", async () => {
    const { container } = await render(
      <Text as="span" data-testid="profile-name">
        Ada Lovelace
      </Text>,
    );

    const element = container.querySelector('[data-testid="profile-name"]');
    expect(element).not.toBeNull();
    expect(element?.textContent).toBe("Ada Lovelace");
  });
});

describe("Text type scale", () => {
  it.each(AS_DEFAULT_VARIANT)("derives the type scale for as=%s from %s", async (as, variant) => {
    // Compared render-to-render rather than against literal utilities: the lookup
    // table is what is under test, not the scale's exact class string.
    const derived = await renderedClasses(<Text as={as}>Wallow</Text>);
    const explicit = await renderedClasses(<Text variant={variant}>Wallow</Text>);

    expect(derived).toEqual(explicit);
  });

  it("keeps the as-derived scales distinct from one another", async () => {
    const display = await renderedClasses(<Text as="h1">Wallow</Text>);
    const body = await renderedClasses(<Text as="p">Wallow</Text>);
    const caption = await renderedClasses(<Text as="legend">Wallow</Text>);

    expect(display).not.toEqual(body);
    expect(caption).not.toEqual(body);
  });

  it("lets an explicit variant override the as-derived default", async () => {
    // The decoupling case: an h2 that reads as body copy still renders an <h2>.
    const { container } = await render(
      <Text as="h2" variant="body">
        Wallow
      </Text>,
    );
    const element = only(container);

    expect(element.tagName.toLowerCase()).toBe("h2");
    expect(classSet(element)).toEqual(await renderedClasses(<Text as="p">Wallow</Text>));
    expect(classSet(element)).not.toEqual(await renderedClasses(<Text as="h2">Wallow</Text>));
  });

  it("gives the overline variant the uppercase caption treatment", () => {
    // The string two wallow-web sections hand-roll today, minus the mb-1 (that is
    // the caller's spacing) and minus the /70 (that is the color prop's job).
    const overline = textRecipe({ variant: "overline" }).split(" ");

    expect(overline).toContain("uppercase");
    expect(overline).toContain("tracking-wider");
    expect(overline).not.toContain("mb-1");
  });
});

describe("Text semantic colour", () => {
  it.each(COLOR_TOKENS)("maps color=%s onto %s", (color, token) => {
    expect(textRecipe({ color }).split(" ")).toContain(token);
  });

  it("carries no opacity-suffixed colour utility for any variant and colour", () => {
    // The whole reason Text exists: `text-foreground/60` must be unreachable
    // through the recipe, in every combination it can produce.
    const offenders: string[] = [];

    for (const variant of VARIANTS) {
      for (const [color] of COLOR_TOKENS) {
        for (const className of textRecipe({ variant, color }).split(" ")) {
          if (className.includes("/")) {
            offenders.push(`${variant}/${color}: ${className}`);
          }
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it("resolves bodySm + muted to exactly MutedText's two classes", async () => {
    // Wallow-lrlm.2.2 reroutes MutedText's render through Text, and
    // muted-text.test.tsx asserts an EXACT two-class set with no edits allowed.
    // So the recipe's base string must be empty and bodySm must be text-sm alone.
    const classes = await renderedClasses(
      <Text as="p" variant="bodySm" color="muted">
        Loading…
      </Text>,
    );

    expect(classes).toEqual(["text-muted-foreground", "text-sm"]);
  });
});

describe("Text overrides", () => {
  it("lets a caller className beat the recipe's colour", async () => {
    const classes = await renderedClasses(
      <Text color="muted" className="text-destructive">
        Failed
      </Text>,
    );

    expect(classes).toContain("text-destructive");
    expect(classes).not.toContain("text-muted-foreground");
  });

  it("lets a caller className beat the variant's type scale", async () => {
    const classes = await renderedClasses(
      <Text variant="display" className="text-sm">
        Small display
      </Text>,
    );

    expect(classes).toContain("text-sm");
    expect(classes.filter((name) => /^text-(?:xs|sm|base|lg|\d?xl)$/u.test(name))).toEqual([
      "text-sm",
    ]);
  });

  it("lets the weight prop beat the variant's own weight", async () => {
    const classes = await renderedClasses(
      <Text variant="display" weight="normal">
        Light display
      </Text>,
    );

    // tailwind-merge must collapse the conflict, not stack both weights.
    expect(classes.filter((name) => /^font-(?:normal|medium|semibold|bold)$/u.test(name))).toEqual([
      "font-normal",
    ]);
  });

  it("applies the align prop", async () => {
    const classes = await renderedClasses(<Text align="center">Centred</Text>);

    expect(classes).toContain("text-center");
  });
});
