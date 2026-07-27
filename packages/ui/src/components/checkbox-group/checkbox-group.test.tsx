import { render } from "@bc-solutions-coder/testing/render";
import type { CSSProperties } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Checkbox } from "../checkbox";
import { CheckboxGroup } from "./checkbox-group";

/*
 * Wallow-m5aq.2.5 — Checkbox Group. Same spec shape as the exemplar
 * (Wallow-m5aq.2.1): browser vitest project, nothing mocked, the recipe asserted
 * THROUGH the component, class assertions as an order-free set.
 *
 * This file imports `Checkbox` from the sibling folder because a group is only
 * observable through the boxes it drives — the two components share no import at
 * RUNTIME (they meet through Base UI's context), but the spec has to render both
 * to see a value array change.
 *
 * Assertions were measured against the installed Base UI 1.6.0: the group is a
 * `<div role="group">`, a disabled group stamps `data-disabled` on ITSELF AND on
 * every child box, and a `parent` box reports `aria-checked="mixed"` plus
 * `data-indeterminate` while only some of `allValues` are ticked.
 */

/**
 * Tailwind is not compiled in the `browser` vitest project, so a Base UI root
 * span measures 0x0 and `userEvent.click` times out on Playwright's
 * actionability check. Clickable boxes get an explicit inline box; see the same
 * note in checkbox.test.tsx.
 */
const TEST_BOX: CSSProperties = { width: 16, height: 16, display: "inline-block" };

/** Every utility `CheckboxGroup` must render. */
const GROUP_CLASSES = ["flex", "flex-col", "gap-2", "data-[disabled]:opacity-50"];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function group(container: HTMLElement): HTMLElement {
  const element = container.querySelector<HTMLElement>('[role="group"]');
  expect(element).not.toBeNull();
  return element as HTMLElement;
}

function box(container: HTMLElement, testId: string): HTMLElement {
  const element = container.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
  expect(element, testId).not.toBeNull();
  return element as HTMLElement;
}

describe("CheckboxGroup", () => {
  it("renders a role=group div with the group recipe class set", async () => {
    const { container } = await render(
      <CheckboxGroup>
        <Checkbox.Root name="a" data-testid="a" />
      </CheckboxGroup>,
    );

    expect(group(container).tagName).toBe("DIV");
    expect(classSet(group(container))).toEqual(GROUP_CLASSES.toSorted());
  });

  it("ticks the boxes named in defaultValue", async () => {
    const { container } = await render(
      <CheckboxGroup defaultValue={["a"]}>
        <Checkbox.Root name="a" data-testid="a" />
        <Checkbox.Root name="b" data-testid="b" />
      </CheckboxGroup>,
    );

    expect(box(container, "a").hasAttribute("data-checked")).toBe(true);
    expect(box(container, "b").hasAttribute("data-unchecked")).toBe(true);
  });

  it("reports the whole new value array to onValueChange", async () => {
    // The group's contract is an ARRAY of ticked names, not a single toggle —
    // pinned here because it is the one thing a caller wires a form to.
    const onValueChange = vi.fn();
    const { container } = await render(
      <CheckboxGroup defaultValue={["a"]} onValueChange={onValueChange}>
        <Checkbox.Root style={TEST_BOX} name="a" data-testid="a" />
        <Checkbox.Root style={TEST_BOX} name="b" data-testid="b" />
      </CheckboxGroup>,
    );

    await userEvent.click(box(container, "b"));

    expect(onValueChange).toHaveBeenCalledTimes(1);
    expect(onValueChange.mock.calls[0]?.[0]).toEqual(["a", "b"]);
    expect(box(container, "b").hasAttribute("data-checked")).toBe(true);
  });

  it("toggles a child from the keyboard", async () => {
    const onValueChange = vi.fn();
    const { container } = await render(
      <CheckboxGroup defaultValue={[]} onValueChange={onValueChange}>
        <Checkbox.Root name="a" data-testid="a" />
      </CheckboxGroup>,
    );

    box(container, "a").focus();
    await userEvent.keyboard(" ");

    expect(onValueChange.mock.calls[0]?.[0]).toEqual(["a"]);
    expect(box(container, "a").hasAttribute("data-checked")).toBe(true);
  });

  it("stays put when the group is controlled and the caller ignores the change", async () => {
    // A controlled `value` with no onValueChange handler must not self-update —
    // the proof that `value` is genuinely controlled rather than an initial seed.
    const { container } = await render(
      <CheckboxGroup value={["a"]}>
        <Checkbox.Root style={TEST_BOX} name="a" data-testid="a" />
        <Checkbox.Root style={TEST_BOX} name="b" data-testid="b" />
      </CheckboxGroup>,
    );

    await userEvent.click(box(container, "b"));

    expect(box(container, "b").hasAttribute("data-unchecked")).toBe(true);
    expect(box(container, "a").hasAttribute("data-checked")).toBe(true);
  });

  it("propagates disabled to the group and every box inside it", async () => {
    const { container } = await render(
      <CheckboxGroup disabled>
        <Checkbox.Root name="a" data-testid="a" />
        <Checkbox.Root name="b" data-testid="b" />
      </CheckboxGroup>,
    );

    expect(group(container).hasAttribute("data-disabled")).toBe(true);
    expect(box(container, "a").hasAttribute("data-disabled")).toBe(true);
    expect(box(container, "b").getAttribute("aria-disabled")).toBe("true");
  });

  it("drives a parent checkbox into the mixed state from allValues", async () => {
    // The headline Checkbox Group feature: a `parent` box summarises the others,
    // which is exactly where `data-[indeterminate]:` on the checkbox recipe earns
    // its keep.
    const { container } = await render(
      <CheckboxGroup defaultValue={["a"]} allValues={["a", "b"]}>
        <Checkbox.Root parent data-testid="parent" />
        <Checkbox.Root name="a" data-testid="a" />
        <Checkbox.Root name="b" data-testid="b" />
      </CheckboxGroup>,
    );

    const parent = box(container, "parent");
    expect(parent.hasAttribute("data-parent")).toBe(true);
    expect(parent.hasAttribute("data-indeterminate")).toBe(true);
    expect(parent.getAttribute("aria-checked")).toBe("mixed");
  });

  it("ticks every box when the parent checkbox is ticked", async () => {
    const { container } = await render(
      <CheckboxGroup defaultValue={["a"]} allValues={["a", "b"]}>
        <Checkbox.Root style={TEST_BOX} parent data-testid="parent" />
        <Checkbox.Root style={TEST_BOX} name="a" data-testid="a" />
        <Checkbox.Root style={TEST_BOX} name="b" data-testid="b" />
      </CheckboxGroup>,
    );

    await userEvent.click(box(container, "parent"));

    expect(box(container, "parent").hasAttribute("data-checked")).toBe(true);
    expect(box(container, "a").hasAttribute("data-checked")).toBe(true);
    expect(box(container, "b").hasAttribute("data-checked")).toBe(true);
  });

  it("lets a caller className override a recipe utility", async () => {
    // The cn()/tailwind-merge proof for this component: the conflicting layout
    // utility is REMOVED rather than appended-after.
    const { container } = await render(
      <CheckboxGroup className="flex-row">
        <Checkbox.Root name="a" data-testid="a" />
      </CheckboxGroup>,
    );

    const wrapper = group(container);
    expect(wrapper.classList.contains("flex-row")).toBe(true);
    expect(wrapper.classList.contains("flex-col")).toBe(false);
    expect(wrapper.classList.contains("gap-2")).toBe(true);
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(
      <CheckboxGroup data-testid="signup-topics">
        <Checkbox.Root name="a" data-testid="a" />
      </CheckboxGroup>,
    );

    expect(container.querySelector('[data-testid="signup-topics"]')).not.toBeNull();
  });
});
