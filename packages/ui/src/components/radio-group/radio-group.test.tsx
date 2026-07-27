import { render } from "@bc-solutions-coder/testing/render";
import type { CSSProperties } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Radio } from "../radio";
import { RadioGroup } from "./radio-group";

/*
 * RadioGroup behavioural spec (Wallow-m5aq.2.6), shaped after the
 * Wallow-m5aq.2.1 Button exemplar: browser project, nothing mocked, recipes
 * asserted THROUGH the component, class assertions as an order-free set.
 *
 * The group owns everything that is not one radio's own business — the
 * `radiogroup` role, the selected value, the shared `name`, and roving-focus
 * keyboard selection — so that is what this file covers. Per-radio state lives
 * in ../radio/radio.test.tsx.
 *
 * HIT-TARGET NOTE: `Radio.Root` renders a `<span role="radio">` with no
 * intrinsic size, and Tailwind is not compiled in the `browser` vitest project,
 * so the root recipe's `size-4` is an inert class name here — the span still
 * measures 0x0 and `userEvent.click` times out on Playwright's actionability
 * check instead of failing. Clicked radios therefore get an explicit inline box;
 * see the same note in ../checkbox-group/checkbox-group.test.tsx. Keyboard
 * selection needs no layout, which is why the arrow-key test asserts against
 * the same markup without one.
 */

/** The inline box a clicked `Radio.Root` needs; see the HIT-TARGET NOTE above. */
const TEST_BOX: CSSProperties = { width: 16, height: 16, display: "inline-block" };

/** Utilities the group must render, per orientation. */
const BASE_CLASSES = ["flex", "data-[disabled]:opacity-50"];
const ORIENTATION_CLASSES = {
  vertical: ["flex-col", "gap-2"],
  horizontal: ["flex-row", "gap-4"],
} as const;

/** The full expected class set for an orientation, order-free. */
function expectedClasses(orientation: keyof typeof ORIENTATION_CLASSES): string[] {
  return [...BASE_CLASSES, ...ORIENTATION_CLASSES[orientation]].toSorted();
}

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/** The element carrying `data-testid`, failing loudly rather than returning null. */
function byTestId(container: HTMLElement, testId: string): HTMLElement {
  const element = container.querySelector(`[data-testid="${testId}"]`);
  expect(element, testId).not.toBeNull();
  return element as HTMLElement;
}

/** The two radios every case in this file groups. */
function fruitRadios() {
  return (
    <>
      <Radio.Root style={TEST_BOX} value="apple" aria-label="Apple" data-testid="apple">
        <Radio.Indicator />
      </Radio.Root>
      <Radio.Root style={TEST_BOX} value="pear" aria-label="Pear" data-testid="pear">
        <Radio.Indicator />
      </Radio.Root>
    </>
  );
}

describe("RadioGroup", () => {
  it("renders the vertical recipe by default", async () => {
    const { container } = await render(
      <RadioGroup name="fruit" aria-label="Fruit" data-testid="fruit">
        {fruitRadios()}
      </RadioGroup>,
    );

    const group = byTestId(container, "fruit");
    expect(group.getAttribute("role")).toBe("radiogroup");
    expect(classSet(group)).toEqual(expectedClasses("vertical"));
  });

  it("switches to the horizontal orientation", async () => {
    const { container } = await render(
      <RadioGroup name="fruit" aria-label="Fruit" orientation="horizontal" data-testid="fruit">
        {fruitRadios()}
      </RadioGroup>,
    );

    expect(classSet(byTestId(container, "fruit"))).toEqual(expectedClasses("horizontal"));
  });

  it("lets a caller className override a recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting utility is REMOVED rather
    // than appended-after, and the utilities the caller never mentioned survive.
    const { container } = await render(
      <RadioGroup name="fruit" aria-label="Fruit" className="gap-8" data-testid="fruit">
        {fruitRadios()}
      </RadioGroup>,
    );

    const group = byTestId(container, "fruit");
    expect(group.classList.contains("gap-8")).toBe(true);
    expect(group.classList.contains("gap-2")).toBe(false);
    expect(group.classList.contains("flex-col")).toBe(true);
    expect(group.classList.contains("flex")).toBe(true);
  });

  it("exposes the disabled state as a data attribute and passes it to its radios", async () => {
    const { container } = await render(
      <RadioGroup name="fruit" aria-label="Fruit" disabled data-testid="fruit">
        {fruitRadios()}
      </RadioGroup>,
    );

    expect(byTestId(container, "fruit").getAttribute("data-disabled")).toBe("");
    expect(byTestId(container, "apple").getAttribute("data-disabled")).toBe("");
  });

  it("carries no disabled data attribute when enabled", async () => {
    const { container } = await render(
      <RadioGroup name="fruit" aria-label="Fruit" data-testid="fruit">
        {fruitRadios()}
      </RadioGroup>,
    );

    expect(byTestId(container, "fruit").hasAttribute("data-disabled")).toBe(false);
  });

  it("exposes the readOnly and required states as data attributes", async () => {
    const { container: readOnly } = await render(
      <RadioGroup name="fruit" aria-label="Fruit" readOnly data-testid="fruit">
        {fruitRadios()}
      </RadioGroup>,
    );
    expect(byTestId(readOnly, "fruit").getAttribute("data-readonly")).toBe("");

    const { container: required } = await render(
      <RadioGroup name="fruit" aria-label="Fruit" required data-testid="fruit">
        {fruitRadios()}
      </RadioGroup>,
    );
    expect(byTestId(required, "fruit").getAttribute("data-required")).toBe("");
  });

  it("reports the newly selected value through onValueChange", async () => {
    const onValueChange = vi.fn();
    const { container } = await render(
      <RadioGroup name="fruit" aria-label="Fruit" onValueChange={onValueChange} data-testid="fruit">
        {fruitRadios()}
      </RadioGroup>,
    );

    const pear = byTestId(container, "pear");
    await userEvent.click(pear);

    expect(onValueChange).toHaveBeenCalledTimes(1);
    expect(onValueChange.mock.calls[0]?.[0]).toBe("pear");
    expect(pear.getAttribute("data-checked")).toBe("");
    expect(byTestId(container, "apple").getAttribute("data-unchecked")).toBe("");
  });

  it("moves the selection with the arrow keys", async () => {
    // Roving focus is the group's job, and the reason a radio group is a single
    // tab stop rather than two.
    const onValueChange = vi.fn();
    const { container } = await render(
      <RadioGroup
        name="fruit"
        aria-label="Fruit"
        defaultValue="apple"
        onValueChange={onValueChange}
        data-testid="fruit"
      >
        {fruitRadios()}
      </RadioGroup>,
    );

    byTestId(container, "apple").focus();
    await userEvent.keyboard("{ArrowDown}");

    expect(byTestId(container, "pear").getAttribute("data-checked")).toBe("");
    expect(byTestId(container, "apple").getAttribute("data-unchecked")).toBe("");
    expect(onValueChange.mock.calls[0]?.[0]).toBe("pear");
  });

  it("composes onto another element through the render prop", async () => {
    const { container } = await render(
      <RadioGroup name="fruit" aria-label="Fruit" render={<section />} data-testid="fruit">
        {fruitRadios()}
      </RadioGroup>,
    );

    const group = byTestId(container, "fruit");
    expect(group.tagName).toBe("SECTION");
    expect(group.getAttribute("role")).toBe("radiogroup");
    expect(classSet(group)).toEqual(expectedClasses("vertical"));
  });
});
