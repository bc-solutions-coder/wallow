import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Toggle, type ToggleProps } from "./toggle";

/*
 * Wallow-m5aq.2.12 — Toggle. Same spec shape as the exemplar
 * (Wallow-m5aq.2.1): browser vitest project, nothing mocked, the recipe asserted
 * THROUGH the component, class assertions as an order-free set.
 *
 * Unlike the Switch/Checkbox specs, this one CAN use `userEvent.click`. Tailwind
 * is not compiled in the `browser` project, so a component whose box comes only
 * from its recipe measures 0x0 and Playwright's actionability check refuses to
 * click it — but a Toggle is a `<button>` with a label inside, and text gives it
 * intrinsic size. Every toggle rendered below therefore has children.
 *
 * One measured trap does apply, and it is why the pointer assertions await:
 * reading the DOM immediately after a synchronous `element.click()` sees the
 * PRE-click attributes here (the spy has already fired, the attribute has not
 * landed). `await userEvent.click(...)` yields long enough for React to flush,
 * so it is the only pointer path used.
 *
 * Every assertion below was measured against the unstyled Base UI 1.6.0 part
 * before this spec was written. Two of those measurements are worth stating
 * because they are easy to assume wrong:
 *   - there is NO `data-unpressed`. Off is the ABSENCE of `data-pressed`, which
 *     is why the recipe can only style the pressed state.
 *   - a STANDALONE disabled toggle gets the native `disabled` attribute and no
 *     `aria-disabled`. (Inside a ToggleGroup it gets both; that belongs to
 *     toggle-group.test.tsx.)
 */

/** Every utility `Toggle` must render. Single source of truth for the recipe. */
const TOGGLE_CLASSES = [
  "inline-flex",
  "items-center",
  "justify-center",
  "gap-2",
  "rounded-md",
  "px-3",
  "py-2",
  "text-sm",
  "font-medium",
  "text-foreground",
  "transition-colors",
  "data-[pressed]:bg-accent",
  "data-[pressed]:text-accent-foreground",
  "data-[disabled]:opacity-50",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

async function renderToggle(props: ToggleProps = {}): Promise<HTMLElement> {
  const { container } = await render(
    <Toggle data-testid="toggle" {...props}>
      Bold
    </Toggle>,
  );

  const toggle = container.querySelector<HTMLElement>('[data-testid="toggle"]');
  expect(toggle, "toggle").not.toBeNull();
  return toggle as HTMLElement;
}

describe("Toggle", () => {
  it("renders a native button carrying the recipe class set", async () => {
    const toggle = await renderToggle();

    expect(toggle.tagName).toBe("BUTTON");
    expect(toggle.getAttribute("type")).toBe("button");
    expect(classSet(toggle)).toEqual(TOGGLE_CLASSES.toSorted());
  });

  it("starts unpressed, with off expressed as the absence of data-pressed", async () => {
    const toggle = await renderToggle();

    expect(toggle.getAttribute("aria-pressed")).toBe("false");
    expect(toggle.hasAttribute("data-pressed")).toBe(false);
  });

  it("honours defaultPressed", async () => {
    const toggle = await renderToggle({ defaultPressed: true });

    expect(toggle.getAttribute("aria-pressed")).toBe("true");
    expect(toggle.hasAttribute("data-pressed")).toBe(true);
  });

  it("toggles on click, and back again", async () => {
    const toggle = await renderToggle();
    expect(toggle.hasAttribute("data-pressed")).toBe(false);

    await userEvent.click(toggle);

    expect(toggle.getAttribute("aria-pressed")).toBe("true");
    expect(toggle.hasAttribute("data-pressed")).toBe(true);

    await userEvent.click(toggle);

    expect(toggle.getAttribute("aria-pressed")).toBe("false");
    expect(toggle.hasAttribute("data-pressed")).toBe(false);
  });

  it("reports the new pressed state to onPressedChange", async () => {
    const onPressedChange = vi.fn();
    const toggle = await renderToggle({ onPressedChange });

    await userEvent.click(toggle);

    expect(onPressedChange).toHaveBeenCalledTimes(1);
    expect(onPressedChange.mock.calls[0]?.[0]).toBe(true);
  });

  it("toggles from the keyboard with both Space and Enter", async () => {
    // A native <button> would fire click for both, but Base UI installs its own
    // key handling on the way to publishing `data-pressed`, so both keys are
    // pinned rather than assumed.
    const toggle = await renderToggle();

    toggle.focus();
    await userEvent.keyboard(" ");
    expect(toggle.hasAttribute("data-pressed")).toBe(true);

    await userEvent.keyboard("{Enter}");
    expect(toggle.hasAttribute("data-pressed")).toBe(false);
  });

  it("leaves a controlled toggle for its owner to update", async () => {
    // `pressed` without a state update must NOT move: the component may not keep
    // private state behind the caller's back.
    const onPressedChange = vi.fn();
    const toggle = await renderToggle({ pressed: false, onPressedChange });

    await userEvent.click(toggle);

    expect(onPressedChange.mock.calls[0]?.[0]).toBe(true);
    expect(toggle.getAttribute("aria-pressed")).toBe("false");
    expect(toggle.hasAttribute("data-pressed")).toBe(false);
  });

  it("exposes the disabled state and refuses to toggle", async () => {
    const onPressedChange = vi.fn();
    const toggle = await renderToggle({ disabled: true, onPressedChange });

    expect(toggle.hasAttribute("data-disabled")).toBe(true);
    expect((toggle as HTMLButtonElement).disabled).toBe(true);

    await userEvent.click(toggle, { force: true });

    expect(onPressedChange).not.toHaveBeenCalled();
    expect(toggle.hasAttribute("data-pressed")).toBe(false);
  });

  it("lets a caller className override a recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting utility is REMOVED rather
    // than appended after, and the utilities the caller never mentioned — the
    // rest of the box and the pressed-state colour — survive. A string-append
    // implementation leaves both `px-3` and `px-6` on the element.
    const toggle = await renderToggle({ className: "px-6" });

    expect(toggle.classList.contains("px-6")).toBe(true);
    expect(toggle.classList.contains("px-3")).toBe(false);
    expect(toggle.classList.contains("py-2")).toBe(true);
    expect(toggle.classList.contains("data-[pressed]:bg-accent")).toBe(true);
  });

  it("composes the recipe onto another element through the render prop", async () => {
    // `nativeButton={false}` tells Base UI the substituted element is not a
    // <button>, so it adds role="button" and the keyboard handling a link lacks.
    const { container } = await render(
      <Toggle render={<a href="#bold" />} nativeButton={false} defaultPressed>
        Bold
      </Toggle>,
    );

    const link = container.querySelector("a");
    expect(link?.getAttribute("role")).toBe("button");
    expect(link?.getAttribute("aria-pressed")).toBe("true");
    expect(classSet(link as Element)).toEqual(TOGGLE_CLASSES.toSorted());
    expect(container.querySelector("button")).toBeNull();
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Toggle data-testid="editor-bold">Bold</Toggle>);

    expect(container.querySelector('[data-testid="editor-bold"]')).not.toBeNull();
  });
});
