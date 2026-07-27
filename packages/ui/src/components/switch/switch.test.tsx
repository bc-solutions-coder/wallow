import { render } from "@bc-solutions-coder/testing/render";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";

import { Switch, type SwitchRootProps } from "./switch";

/*
 * Follows the exemplar spec shape from Wallow-m5aq.2.1 (button.test.tsx):
 * browser project, nothing mocked, recipes asserted THROUGH the component, and
 * class assertions as an order-free SET so tailwind-merge may reorder.
 *
 * ONE DELIBERATE DEPARTURE, and it is not stylistic. The exemplar drives its
 * interaction with `userEvent.click`. A Switch cannot be clicked that way in
 * this project: Tailwind's stylesheet is NOT loaded in the vitest browser
 * project (verified — a `h-6 w-11` span measures 0x0 here), and a switch has no
 * text to give it intrinsic size, so the root is a 0x0 box that Playwright's
 * actionability check refuses to click. `userEvent.click` therefore hangs for
 * the full timeout and fails no matter how correct the component is.
 *
 * So pointer activation is dispatched with the DOM's own `root.click()`, which
 * runs React's handler without an actionability gate, and keyboard activation
 * goes through the real `userEvent.keyboard` against the focused root. Both
 * were verified against the unstyled Base UI part before this spec was written.
 * The *visual* half — that the track really is 44x24 with the thumb parked at
 * the correct end — belongs to `switch.stories.tsx`, which renders under the
 * real Tailwind pipeline.
 */

/** The track's utilities. Single source of truth for the class assertions. */
const ROOT_CLASSES = [
  "inline-flex",
  "h-6",
  "w-11",
  "shrink-0",
  "cursor-pointer",
  "items-center",
  "rounded-full",
  "bg-input",
  "p-0.5",
  "transition-colors",
  "data-[checked]:bg-primary",
  "data-[disabled]:opacity-50",
];

/** The thumb's utilities. */
const THUMB_CLASSES = [
  "block",
  "size-5",
  "rounded-full",
  "bg-background",
  "transition-transform",
  "data-[checked]:translate-x-5",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

interface RenderedSwitch {
  readonly container: HTMLElement;
  readonly root: HTMLElement;
  readonly thumb: HTMLElement;
  /** The visually hidden checkbox Base UI submits with the form. */
  readonly input: HTMLInputElement;
}

async function renderSwitch(props: SwitchRootProps = {}): Promise<RenderedSwitch> {
  const { container } = await render(
    <Switch.Root {...props}>
      <Switch.Thumb data-testid="switch-thumb" />
    </Switch.Root>,
  );

  const root = container.querySelector('[role="switch"]');
  const thumb = container.querySelector('[data-testid="switch-thumb"]');
  const input = container.querySelector('input[type="checkbox"]');
  expect(root, "root").not.toBeNull();
  expect(thumb, "thumb").not.toBeNull();
  expect(input, "hidden input").not.toBeNull();

  return {
    container,
    root: root as HTMLElement,
    thumb: thumb as HTMLElement,
    input: input as HTMLInputElement,
  };
}

describe("Switch", () => {
  it("renders the track recipe on the switch element", async () => {
    const { root } = await renderSwitch();

    expect(classSet(root)).toEqual(ROOT_CLASSES.toSorted());
  });

  it("renders the thumb recipe on the thumb element", async () => {
    const { thumb } = await renderSwitch();

    expect(classSet(thumb)).toEqual(THUMB_CLASSES.toSorted());
  });

  it("starts unchecked, with both parts carrying the unchecked state", async () => {
    // Base UI publishes the off state as its own attribute rather than the
    // absence of one, and the thumb mirrors the root's state through context —
    // which is what lets the thumb's travel distance live in the thumb recipe.
    const { root, thumb } = await renderSwitch();

    expect(root.getAttribute("aria-checked")).toBe("false");
    for (const part of [root, thumb]) {
      expect(part.hasAttribute("data-unchecked")).toBe(true);
      expect(part.hasAttribute("data-checked")).toBe(false);
    }
  });

  it("honours defaultChecked on both parts", async () => {
    const { root, thumb } = await renderSwitch({ defaultChecked: true });

    expect(root.getAttribute("aria-checked")).toBe("true");
    for (const part of [root, thumb]) {
      expect(part.hasAttribute("data-checked")).toBe(true);
      expect(part.hasAttribute("data-unchecked")).toBe(false);
    }
  });

  it("toggles both parts on activation, and back again", async () => {
    const { root, thumb } = await renderSwitch();

    root.click();

    expect(root.getAttribute("aria-checked")).toBe("true");
    expect(root.hasAttribute("data-checked")).toBe(true);
    expect(thumb.hasAttribute("data-checked")).toBe(true);

    root.click();

    expect(root.hasAttribute("data-checked")).toBe(false);
    expect(thumb.hasAttribute("data-unchecked")).toBe(true);
  });

  it("toggles from the keyboard", async () => {
    // The root is a <span role="switch">, not a native control, so Base UI has
    // to re-implement Space activation itself. That is worth pinning.
    const { root, thumb } = await renderSwitch();

    root.focus();
    await userEvent.keyboard(" ");

    expect(root.hasAttribute("data-checked")).toBe(true);
    expect(thumb.hasAttribute("data-checked")).toBe(true);
  });

  it("reports the new state to onCheckedChange", async () => {
    const onCheckedChange = vi.fn();
    const { root } = await renderSwitch({ onCheckedChange });

    root.click();

    expect(onCheckedChange).toHaveBeenCalledTimes(1);
    expect(onCheckedChange.mock.calls[0]?.[0]).toBe(true);
  });

  it("leaves a controlled switch for its owner to update", async () => {
    // `checked` without a state update must NOT move: the component may not
    // keep private state behind the caller's back.
    const onCheckedChange = vi.fn();
    const { root, thumb } = await renderSwitch({ checked: false, onCheckedChange });

    root.click();

    expect(onCheckedChange.mock.calls[0]?.[0]).toBe(true);
    expect(root.hasAttribute("data-unchecked")).toBe(true);
    expect(thumb.hasAttribute("data-checked")).toBe(false);
  });

  it("exposes the disabled state on both parts and refuses to toggle", async () => {
    const onCheckedChange = vi.fn();
    const { root, thumb, input } = await renderSwitch({ disabled: true, onCheckedChange });

    expect(root.getAttribute("aria-disabled")).toBe("true");
    expect(input.disabled).toBe(true);
    for (const part of [root, thumb]) {
      expect(part.hasAttribute("data-disabled")).toBe(true);
    }

    root.click();

    expect(onCheckedChange).not.toHaveBeenCalled();
    expect(root.hasAttribute("data-checked")).toBe(false);
  });

  it("exposes the readOnly state on both parts and refuses to toggle", async () => {
    const onCheckedChange = vi.fn();
    const { root, thumb } = await renderSwitch({ readOnly: true, onCheckedChange });

    expect(root.getAttribute("aria-readonly")).toBe("true");
    for (const part of [root, thumb]) {
      expect(part.hasAttribute("data-readonly")).toBe(true);
    }

    root.click();

    expect(onCheckedChange).not.toHaveBeenCalled();
    expect(root.hasAttribute("data-checked")).toBe(false);
  });

  it("exposes the required state on both parts", async () => {
    const { root, thumb, input } = await renderSwitch({ required: true });

    expect(root.getAttribute("aria-required")).toBe("true");
    expect(input.required).toBe(true);
    for (const part of [root, thumb]) {
      expect(part.hasAttribute("data-required")).toBe(true);
    }
  });

  it("submits its value through the hidden checkbox input", async () => {
    // The reason a Switch is form-usable at all: the visible part is a <span>,
    // so name/value have to reach a real control.
    const { input } = await renderSwitch({ name: "notify", value: "yes", defaultChecked: true });

    expect(input.name).toBe("notify");
    expect(input.value).toBe("yes");
    expect(input.checked).toBe(true);
  });

  it("lets a caller className override a track recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting utility is REMOVED rather
    // than appended after, and the utilities the caller never mentioned — the
    // shape and the checked-state colour — survive. A string-append
    // implementation leaves both `bg-input` and `bg-accent` on the element.
    const { root } = await renderSwitch({ className: "bg-accent" });

    expect(root.classList.contains("bg-accent")).toBe(true);
    expect(root.classList.contains("bg-input")).toBe(false);
    expect(root.classList.contains("rounded-full")).toBe(true);
    expect(root.classList.contains("data-[checked]:bg-primary")).toBe(true);
  });

  it("lets a caller className override a thumb recipe utility", async () => {
    // Asserted separately from the track: each part wires its own cn(), and a
    // component that merged only the root would still pass the test above.
    const { container } = await render(
      <Switch.Root>
        <Switch.Thumb className="bg-accent" data-testid="switch-thumb" />
      </Switch.Root>,
    );

    const thumb = container.querySelector('[data-testid="switch-thumb"]') as HTMLElement;
    expect(thumb.classList.contains("bg-accent")).toBe(true);
    expect(thumb.classList.contains("bg-background")).toBe(false);
    expect(thumb.classList.contains("size-5")).toBe(true);
  });

  it("composes both parts onto other elements through the render prop", async () => {
    // `nativeButton` tells Base UI the substituted element really is a
    // <button>, which is what stops it logging a dev-mode error.
    const { container } = await render(
      <Switch.Root render={<button type="button" />} nativeButton>
        <Switch.Thumb render={<i />} data-testid="switch-thumb" />
      </Switch.Root>,
    );

    const button = container.querySelector("button");
    const thumb = container.querySelector("i");
    expect(button?.getAttribute("role")).toBe("switch");
    expect(classSet(button as Element)).toEqual(ROOT_CLASSES.toSorted());
    expect(classSet(thumb as Element)).toEqual(THUMB_CLASSES.toSorted());
    expect(container.querySelector('span[role="switch"]')).toBeNull();
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(
      <Switch.Root data-testid="settings-notify">
        <Switch.Thumb />
      </Switch.Root>,
    );

    expect(container.querySelector('[data-testid="settings-notify"]')).not.toBeNull();
  });
});
