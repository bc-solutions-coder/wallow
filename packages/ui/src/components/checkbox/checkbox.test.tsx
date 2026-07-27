import { render } from "@bc-solutions-coder/testing/render";
import type { CSSProperties } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Checkbox } from "./checkbox";

/*
 * Wallow-m5aq.2.5 — Checkbox. Follows the exemplar spec shape (Wallow-m5aq.2.1,
 * see button.test.tsx): browser vitest project, nothing mocked, the recipe
 * asserted THROUGH the component rather than by importing the recipe, and class
 * assertions written as an ORDER-FREE SET because cn()/tailwind-merge may
 * reorder.
 *
 * Base UI's actual `Checkbox` DOM was measured against the installed 1.6.0
 * before these assertions were written, so they pin observed behaviour:
 *
 *   <span role="checkbox" tabindex="0" aria-checked="false" data-unchecked>…</span>
 *   <input type="checkbox" aria-hidden="true" tabindex="-1" …>   <-- SIBLING, not a child
 *
 * Three consequences worth knowing before editing this file:
 *   - the hidden <input> is a SIBLING of the root span, so `container` holds two
 *     element children for a single checkbox;
 *   - the Indicator is UNMOUNTED while the box is unticked unless `keepMounted`;
 *   - state is published as `data-checked` / `data-unchecked` /
 *     `data-indeterminate` / `data-disabled` / `data-readonly` / `data-required`,
 *     which is what the recipe's `data-[…]:` modifiers hang off.
 */

/**
 * Tailwind is NOT compiled in the `browser` vitest project (only the `storybook`
 * project runs the real Tailwind pipeline), so `size-4` produces no layout here
 * and Base UI's root span measures 0x0. Playwright's actionability check then
 * never settles and `userEvent.click` times out after ~15s.
 *
 * Interaction tests therefore either drive the keyboard (which needs no box) or
 * give the root an explicit inline box with this style. Base UI passes `style`
 * straight through to the rendered element.
 */
const TEST_BOX: CSSProperties = { width: 16, height: 16, display: "inline-block" };

/** Every utility `Checkbox.Root` must render. */
const ROOT_CLASSES = [
  "inline-flex",
  "size-4",
  "shrink-0",
  "items-center",
  "justify-center",
  "rounded-sm",
  "border",
  "border-input",
  "bg-background",
  "data-[checked]:border-primary",
  "data-[checked]:bg-primary",
  "data-[indeterminate]:border-primary",
  "data-[indeterminate]:bg-primary",
  "data-[disabled]:opacity-50",
];

/** Every utility `Checkbox.Indicator` must render. */
const INDICATOR_CLASSES = ["flex", "items-center", "justify-center", "text-primary-foreground"];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/** The root span — Base UI marks it `role="checkbox"`. */
function root(container: HTMLElement): HTMLElement {
  const element = container.querySelector<HTMLElement>('[role="checkbox"]');
  expect(element).not.toBeNull();
  return element as HTMLElement;
}

/** The hidden input Base UI renders beside the root for form submission. */
function hiddenInput(container: HTMLElement): HTMLInputElement {
  const element = container.querySelector<HTMLInputElement>('input[type="checkbox"]');
  expect(element).not.toBeNull();
  return element as HTMLInputElement;
}

/** The tick mark, or null while Base UI keeps it unmounted. */
function indicator(container: HTMLElement): HTMLElement | null {
  return container.querySelector<HTMLElement>('[data-testid="tick"]');
}

describe("Checkbox", () => {
  it("exposes exactly Base UI's part names on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A part added
    // here that Base UI does not have (or a missing one) fails.
    expect(Object.keys(Checkbox).toSorted()).toEqual(["Indicator", "Root"]);
  });

  it("renders the root recipe as an order-free class set", async () => {
    const { container } = await render(<Checkbox.Root />);

    expect(classSet(root(container))).toEqual(ROOT_CLASSES.toSorted());
  });

  it("renders a role=checkbox span with the hidden input beside it", async () => {
    // Base UI's structural contract, and the reason `root()` selects by role
    // rather than taking `container.firstElementChild`.
    const { container } = await render(<Checkbox.Root />);

    expect(root(container).tagName).toBe("SPAN");
    expect(hiddenInput(container).getAttribute("aria-hidden")).toBe("true");
    expect(hiddenInput(container).parentElement).toBe(root(container).parentElement);
  });

  it("marks an unticked box with data-unchecked", async () => {
    const { container } = await render(<Checkbox.Root />);

    expect(root(container).hasAttribute("data-unchecked")).toBe(true);
    expect(root(container).hasAttribute("data-checked")).toBe(false);
    expect(root(container).getAttribute("aria-checked")).toBe("false");
  });

  it("marks a ticked box with data-checked", async () => {
    const { container } = await render(<Checkbox.Root defaultChecked />);

    expect(root(container).hasAttribute("data-checked")).toBe(true);
    expect(root(container).hasAttribute("data-unchecked")).toBe(false);
    expect(root(container).getAttribute("aria-checked")).toBe("true");
  });

  it("reports the mixed state as data-indeterminate and aria-checked=mixed", async () => {
    // The third state the bead calls out. `data-[indeterminate]:` in the recipe
    // is what paints it, so both halves are pinned together.
    const { container } = await render(<Checkbox.Root indeterminate />);

    expect(root(container).hasAttribute("data-indeterminate")).toBe(true);
    expect(root(container).getAttribute("aria-checked")).toBe("mixed");
  });

  it("exposes disabled as data-disabled and drops the box from the tab order", async () => {
    const { container } = await render(<Checkbox.Root disabled />);

    expect(root(container).hasAttribute("data-disabled")).toBe(true);
    expect(root(container).getAttribute("aria-disabled")).toBe("true");
    expect(root(container).getAttribute("tabindex")).toBe("-1");
    expect(hiddenInput(container).disabled).toBe(true);
  });

  it("exposes readOnly and required as data attributes", async () => {
    const { container: readOnly } = await render(<Checkbox.Root readOnly />);
    const { container: required } = await render(<Checkbox.Root required />);

    expect(root(readOnly).hasAttribute("data-readonly")).toBe(true);
    expect(root(required).hasAttribute("data-required")).toBe(true);
    expect(root(required).getAttribute("aria-required")).toBe("true");
  });

  it("leaves the indicator unmounted while the box is unticked", async () => {
    // Base UI's default (`keepMounted` is false), so the tick mark cannot be
    // styled into invisibility — it genuinely is not in the DOM.
    const { container } = await render(
      <Checkbox.Root>
        <Checkbox.Indicator data-testid="tick">x</Checkbox.Indicator>
      </Checkbox.Root>,
    );

    expect(indicator(container)).toBeNull();
  });

  it("mounts the indicator with its own recipe once the box is ticked", async () => {
    const { container } = await render(
      <Checkbox.Root defaultChecked>
        <Checkbox.Indicator data-testid="tick">x</Checkbox.Indicator>
      </Checkbox.Root>,
    );

    const tick = indicator(container);
    expect(tick).not.toBeNull();
    expect(classSet(tick as Element)).toEqual(INDICATOR_CLASSES.toSorted());
  });

  it("keeps the indicator mounted while unticked when keepMounted is set", async () => {
    const { container } = await render(
      <Checkbox.Root>
        <Checkbox.Indicator data-testid="tick" keepMounted>
          x
        </Checkbox.Indicator>
      </Checkbox.Root>,
    );

    expect(indicator(container)).not.toBeNull();
    expect(indicator(container)?.hasAttribute("data-unchecked")).toBe(true);
  });

  it("lets a caller className override a root recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both background classes on and fails.
    const { container } = await render(<Checkbox.Root className="bg-accent" />);

    const box = root(container);
    expect(box.classList.contains("bg-accent")).toBe(true);
    expect(box.classList.contains("bg-background")).toBe(false);
    expect(box.classList.contains("border-input")).toBe(true);
    expect(box.classList.contains("rounded-sm")).toBe(true);
  });

  it("lets a caller className override an indicator recipe utility", async () => {
    const { container } = await render(
      <Checkbox.Root defaultChecked>
        <Checkbox.Indicator data-testid="tick" className="text-accent-foreground">
          x
        </Checkbox.Indicator>
      </Checkbox.Root>,
    );

    const tick = indicator(container) as HTMLElement;
    expect(tick.classList.contains("text-accent-foreground")).toBe(true);
    expect(tick.classList.contains("text-primary-foreground")).toBe(false);
    expect(tick.classList.contains("items-center")).toBe(true);
  });

  it("toggles on Space and reports the new state to onCheckedChange", async () => {
    // Keyboard rather than click: no Tailwind here means no box to click (see
    // TEST_BOX above), and Space is the checkbox's native keyboard contract.
    const onCheckedChange = vi.fn();
    const { container } = await render(<Checkbox.Root onCheckedChange={onCheckedChange} />);

    root(container).focus();
    await userEvent.keyboard(" ");

    expect(root(container).hasAttribute("data-checked")).toBe(true);
    expect(onCheckedChange).toHaveBeenCalledTimes(1);
    expect(onCheckedChange.mock.calls[0]?.[0]).toBe(true);
  });

  it("toggles on a pointer click", async () => {
    const onCheckedChange = vi.fn();
    const { container } = await render(
      <Checkbox.Root style={TEST_BOX} onCheckedChange={onCheckedChange} />,
    );

    await userEvent.click(root(container));

    expect(root(container).hasAttribute("data-checked")).toBe(true);
    expect(onCheckedChange.mock.calls[0]?.[0]).toBe(true);
  });

  it("submits its name and value through the hidden input", async () => {
    const { container } = await render(
      <Checkbox.Root name="terms" value="accepted" defaultChecked />,
    );

    const input = hiddenInput(container);
    expect(input.name).toBe("terms");
    expect(input.value).toBe("accepted");
    expect(input.checked).toBe(true);
  });

  it("composes the root onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes.
    const { container } = await render(<Checkbox.Root render={<div />} />);

    const box = root(container);
    expect(box.tagName).toBe("DIV");
    expect(classSet(box)).toEqual(ROOT_CLASSES.toSorted());
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Checkbox.Root data-testid="signup-terms" />);

    expect(container.querySelector('[data-testid="signup-terms"]')).not.toBeNull();
  });
});
