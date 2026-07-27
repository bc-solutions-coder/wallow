import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Toggle } from "../toggle";
import { ToggleGroup } from "./toggle-group";

/*
 * Wallow-m5aq.2.12 — Toggle Group. Same spec shape as the exemplar
 * (Wallow-m5aq.2.1): browser vitest project, nothing mocked, the recipe asserted
 * THROUGH the component, class assertions as an order-free set.
 *
 * This file imports `Toggle` from the sibling folder because a group is only
 * observable through the buttons it drives — the two share no import at RUNTIME
 * (they meet through Base UI's context), but the spec has to render both to see
 * a value array change.
 *
 * Assertions were measured against the installed Base UI 1.6.0. Three of those
 * measurements are worth stating, because each is easy to assume wrong:
 *   - the group always stamps `data-orientation`, even horizontal, while
 *     `data-multiple` appears only when `multiple` is set.
 *   - a toggle inside a group gains `aria-disabled` (a standalone one has only
 *     the native `disabled` attribute), and a disabled GROUP stamps
 *     `data-disabled` on itself and on every child.
 *   - the group is a roving-tabindex composite: exactly one child holds
 *     `tabindex="0"` and the arrow keys move focus, they do not press.
 */

/** Every utility `ToggleGroup` must render. Single source of truth. */
const GROUP_CLASSES = [
  "inline-flex",
  "items-center",
  "gap-1",
  "rounded-md",
  "data-[orientation=vertical]:flex-col",
  "data-[orientation=vertical]:items-stretch",
  "data-[disabled]:opacity-50",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function group(container: HTMLElement): HTMLElement {
  const element = container.querySelector<HTMLElement>('[role="group"]');
  expect(element, "group").not.toBeNull();
  return element as HTMLElement;
}

function toggle(container: HTMLElement, testId: string): HTMLElement {
  const element = container.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
  expect(element, testId).not.toBeNull();
  return element as HTMLElement;
}

/** The two-button group every case below starts from. */
function renderGroup(props: Parameters<typeof ToggleGroup>[0] = {}) {
  return render(
    <ToggleGroup {...props}>
      <Toggle value="bold" data-testid="bold">
        Bold
      </Toggle>
      <Toggle value="italic" data-testid="italic">
        Italic
      </Toggle>
    </ToggleGroup>,
  );
}

describe("ToggleGroup", () => {
  it("renders a role=group div with the group recipe class set", async () => {
    const { container } = await renderGroup();

    expect(group(container).tagName).toBe("DIV");
    expect(classSet(group(container))).toEqual(GROUP_CLASSES.toSorted());
  });

  it("defaults to the horizontal orientation and single-selection mode", async () => {
    // The recipe's vertical modifier keys off `data-orientation`, and its
    // absence in single mode is what keeps `data-multiple` meaningful.
    const { container } = await renderGroup();

    expect(group(container).getAttribute("data-orientation")).toBe("horizontal");
    expect(group(container).hasAttribute("data-multiple")).toBe(false);
  });

  it("presses the toggles named in defaultValue", async () => {
    const { container } = await renderGroup({ defaultValue: ["bold"] });

    expect(toggle(container, "bold").hasAttribute("data-pressed")).toBe(true);
    expect(toggle(container, "bold").getAttribute("aria-pressed")).toBe("true");
    expect(toggle(container, "italic").hasAttribute("data-pressed")).toBe(false);
  });

  it("replaces the pressed toggle in single mode and reports the whole array", async () => {
    // The group's contract is an ARRAY of pressed values, not a single toggle —
    // pinned because it is the one thing a caller wires a toolbar to.
    const onValueChange = vi.fn();
    const { container } = await renderGroup({ defaultValue: ["bold"], onValueChange });
    expect(toggle(container, "italic").hasAttribute("data-pressed")).toBe(false);

    await userEvent.click(toggle(container, "italic"));

    expect(onValueChange).toHaveBeenCalledTimes(1);
    expect(onValueChange.mock.calls[0]?.[0]).toEqual(["italic"]);
    expect(toggle(container, "italic").hasAttribute("data-pressed")).toBe(true);
    expect(toggle(container, "bold").hasAttribute("data-pressed")).toBe(false);
  });

  it("keeps both toggles pressed in multiple mode", async () => {
    const onValueChange = vi.fn();
    const { container } = await renderGroup({
      multiple: true,
      defaultValue: ["bold"],
      onValueChange,
    });
    expect(group(container).hasAttribute("data-multiple")).toBe(true);

    await userEvent.click(toggle(container, "italic"));

    expect(onValueChange.mock.calls[0]?.[0]).toEqual(["bold", "italic"]);
    expect(toggle(container, "bold").hasAttribute("data-pressed")).toBe(true);
    expect(toggle(container, "italic").hasAttribute("data-pressed")).toBe(true);
  });

  it("stays put when the group is controlled and the caller ignores the change", async () => {
    // A controlled `value` with no state update must not self-update — the proof
    // that `value` is genuinely controlled rather than an initial seed.
    const onValueChange = vi.fn();
    const { container } = await renderGroup({ value: ["bold"], onValueChange });

    await userEvent.click(toggle(container, "italic"));

    expect(onValueChange.mock.calls[0]?.[0]).toEqual(["italic"]);
    expect(toggle(container, "italic").hasAttribute("data-pressed")).toBe(false);
    expect(toggle(container, "bold").hasAttribute("data-pressed")).toBe(true);
  });

  it("propagates disabled to the group and every toggle inside it", async () => {
    const { container } = await renderGroup({ disabled: true });

    expect(group(container).hasAttribute("data-disabled")).toBe(true);
    for (const testId of ["bold", "italic"]) {
      expect(toggle(container, testId).hasAttribute("data-disabled"), testId).toBe(true);
      expect(toggle(container, testId).getAttribute("aria-disabled"), testId).toBe("true");
    }
  });

  it("publishes the vertical orientation the recipe keys off", async () => {
    const { container } = await renderGroup({ orientation: "vertical" });

    expect(group(container).getAttribute("data-orientation")).toBe("vertical");
  });

  it("moves focus with the arrow key matching its orientation, without pressing", async () => {
    // A composite widget: one tab stop for the whole group, arrows to move
    // inside it. Worth pinning because it is the behaviour a caller loses if the
    // toggles are ever rendered outside a group by mistake.
    const { container } = await renderGroup({ orientation: "vertical" });

    toggle(container, "bold").focus();
    await userEvent.keyboard("{ArrowDown}");

    expect(document.activeElement).toBe(toggle(container, "italic"));
    expect(toggle(container, "italic").getAttribute("tabindex")).toBe("0");
    expect(toggle(container, "bold").getAttribute("tabindex")).toBe("-1");
    expect(toggle(container, "italic").hasAttribute("data-pressed")).toBe(false);
  });

  it("lets a caller className override a recipe utility", async () => {
    // The cn()/tailwind-merge proof for this component: the conflicting spacing
    // utility is REMOVED rather than appended after.
    const { container } = await renderGroup({ className: "gap-4" });

    const wrapper = group(container);
    expect(wrapper.classList.contains("gap-4")).toBe(true);
    expect(wrapper.classList.contains("gap-1")).toBe(false);
    expect(wrapper.classList.contains("rounded-md")).toBe(true);
  });

  it("composes the recipe onto another element through the render prop", async () => {
    const { container } = await render(
      <ToggleGroup render={<section />}>
        <Toggle value="bold">Bold</Toggle>
      </ToggleGroup>,
    );

    const section = container.querySelector("section");
    expect(section?.getAttribute("role")).toBe("group");
    expect(classSet(section as Element)).toEqual(GROUP_CLASSES.toSorted());
    expect(container.querySelector('div[role="group"]')).toBeNull();
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(
      <ToggleGroup data-testid="editor-marks">
        <Toggle value="bold">Bold</Toggle>
      </ToggleGroup>,
    );

    expect(container.querySelector('[data-testid="editor-marks"]')).not.toBeNull();
  });
});
