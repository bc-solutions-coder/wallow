import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { Separator, type SeparatorProps } from "./separator";

/*
 * Separator behavioural spec (Wallow-m5aq.4.3), shaped after the Wallow-m5aq.2.1
 * Button exemplar:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. The recipe is asserted THROUGH the component, never by importing
 *      `separatorRecipe` and inspecting its return value: a recipe unit test
 *      would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder.
 *   4. Stories carry the visual coverage (see separator.stories.tsx); this file
 *      is only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <div data-orientation="horizontal" role="separator" aria-orientation="horizontal">
 *
 * Two measurements are worth stating because they are easy to assume wrong:
 *
 *   - `aria-orientation` IS EMITTED FOR BOTH orientations, including the
 *     horizontal one ARIA already defaults to. Asserting its absence on a
 *     horizontal separator would fail.
 *   - The element is EMPTY and unstyled: with no recipe it has no size at all,
 *     which is why the recipe owns both the hairline's thickness and its length,
 *     one `data-[orientation=...]:` rule pair each.
 *
 * There is nothing asynchronous about a separator — no portal, no transition, no
 * state — so unlike the overlay specs in this package every assertion here is a
 * plain synchronous read.
 */

/**
 * Utilities `Separator` must render. Both orientation rule pairs ship on EVERY
 * separator: they are CSS modifiers selecting on `data-orientation`, so the
 * class list does not change with the prop — only which pair matches does.
 */
const SEPARATOR_CLASSES = [
  "shrink-0",
  "bg-border",
  "data-[orientation=horizontal]:h-px",
  "data-[orientation=horizontal]:w-full",
  "data-[orientation=vertical]:h-full",
  "data-[orientation=vertical]:w-px",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

async function renderSeparator(props: SeparatorProps = {}): Promise<HTMLElement> {
  const { container } = await render(<Separator data-testid="separator" {...props} />);

  const separator = container.querySelector<HTMLElement>('[data-testid="separator"]');
  expect(separator, "separator").not.toBeNull();
  return separator as HTMLElement;
}

describe("Separator", () => {
  it("renders a div carrying the recipe class set", async () => {
    const separator = await renderSeparator();

    expect(separator.tagName).toBe("DIV");
    expect(classSet(separator)).toEqual(SEPARATOR_CLASSES.toSorted());
  });

  it("is announced as a separator", async () => {
    const separator = await renderSeparator();

    expect(separator.getAttribute("role")).toBe("separator");
  });

  it("defaults to the horizontal orientation", async () => {
    const separator = await renderSeparator();

    expect(separator.getAttribute("data-orientation")).toBe("horizontal");
    expect(separator.getAttribute("aria-orientation")).toBe("horizontal");
  });

  it("publishes an explicit horizontal orientation the same way", async () => {
    const separator = await renderSeparator({ orientation: "horizontal" });

    expect(separator.getAttribute("data-orientation")).toBe("horizontal");
    expect(separator.getAttribute("aria-orientation")).toBe("horizontal");
  });

  it("publishes the vertical orientation, which is what the recipe selects on", async () => {
    const separator = await renderSeparator({ orientation: "vertical" });

    expect(separator.getAttribute("data-orientation")).toBe("vertical");
    expect(separator.getAttribute("aria-orientation")).toBe("vertical");
    // Same class set as the horizontal case: the modifiers do the choosing.
    expect(classSet(separator)).toEqual(SEPARATOR_CLASSES.toSorted());
  });

  it("lets a caller className override a recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, untouched utilities survive, and the
    // modifier-scoped `data-[orientation=...]:` rules are left alone because
    // they are a different tailwind-merge group. A string-append implementation
    // leaves both `bg-border` and `bg-accent` on the element.
    const separator = await renderSeparator({ className: "bg-accent" });

    expect(separator.classList.contains("bg-accent")).toBe(true);
    expect(separator.classList.contains("bg-border")).toBe(false);
    expect(separator.classList.contains("shrink-0")).toBe(true);
    expect(separator.classList.contains("data-[orientation=horizontal]:h-px")).toBe(true);
    expect(separator.classList.contains("data-[orientation=vertical]:w-px")).toBe(true);
  });

  it("composes the recipe onto another element through the render prop", async () => {
    // An <hr> is the semantic element an app reaches for, and the recipe plus
    // the role have to travel to it.
    const separator = await renderSeparator({ render: <hr /> });

    expect(separator.tagName).toBe("HR");
    expect(separator.getAttribute("role")).toBe("separator");
    expect(separator.getAttribute("data-orientation")).toBe("horizontal");
    expect(classSet(separator)).toEqual(SEPARATOR_CLASSES.toSorted());
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Separator data-testid="menu-divider" />);

    expect(container.querySelector('[data-testid="menu-divider"]')).not.toBeNull();
  });
});
