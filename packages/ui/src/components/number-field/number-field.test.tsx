import { render } from "@bc-solutions-coder/testing/render";
import { NumberField as BaseNumberField } from "@base-ui/react/number-field";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { NumberField, type NumberFieldRootProps } from "./number-field";

/*
 * Follows the exemplar spec shape from Wallow-m5aq.2.1 (button.test.tsx):
 * browser project, nothing mocked, recipes asserted THROUGH the component, and
 * class assertions as an order-free SET so tailwind-merge may reorder.
 *
 * THREE DEPARTURES, each forced by how Base UI's number field actually behaves.
 * All three were verified against the real (unstyled) part before this spec was
 * written.
 *
 * 1. Stepper presses are dispatched with the DOM's own `button.click()`, not
 *    `userEvent.click`. Tailwind's stylesheet is NOT loaded in the vitest
 *    browser project, so an unstyled part is a 0x0 box and Playwright's
 *    actionability check refuses to click it — `userEvent.click` then hangs for
 *    the full timeout no matter how correct the component is (Wallow-m5aq.2.7
 *    proved this wave-wide). `click()` also happens to be the path Base UI
 *    handles synchronously: its `shouldSkipClick` guard skips clicks with
 *    `event.detail !== 0`, i.e. REAL pointer clicks, because those are already
 *    served by the press-and-hold pointerdown path. A programmatic `click()`
 *    carries `detail === 0` and therefore steps exactly once.
 *
 * 2. Every read of the value after a step is preceded by `await flush()`. The
 *    number field commits through a ref-backed state update that React has not
 *    applied by the time `click()` returns, so a synchronous read still sees
 *    the OLD value (measured: `input.value` was "2" immediately after the
 *    increment that reported 3 to `onValueChange`).
 *
 * 3. The scrub-area cursor is asserted SYNCHRONOUSLY after `pointerdown`, with
 *    no await. It is portaled into `document.body` and only mounted while
 *    scrubbing, and headless Chromium denies the Pointer Lock request it makes
 *    — asynchronously, ~60ms later, at which point Base UI unmounts the cursor
 *    (measured: present at 5 consecutive ticks, gone at 60ms). Reading it
 *    straight after the pointerdown is the only deterministic window.
 */

/** The root wrapper's utilities. Single source of truth for its assertions. */
const ROOT_CLASSES = ["flex", "w-full", "flex-col", "gap-1.5"];

/** The scrub area's utilities. */
const SCRUB_AREA_CLASSES = [
  "inline-block",
  "cursor-ew-resize",
  "select-none",
  "text-sm",
  "font-medium",
  "text-foreground",
  "data-[disabled]:opacity-50",
];

/** The scrub cursor's utilities. */
const SCRUB_AREA_CURSOR_CLASSES = ["pointer-events-none", "text-foreground", "drop-shadow-sm"];

/** The stepper shell's utilities. */
const GROUP_CLASSES = [
  "inline-flex",
  "items-center",
  "overflow-hidden",
  "rounded-md",
  "border",
  "border-border",
  "bg-background",
  "data-[disabled]:opacity-50",
];

/** The shared stepper-button utilities; the two buttons differ only by border side. */
const STEPPER_CLASSES = [
  "inline-flex",
  "size-9",
  "shrink-0",
  "items-center",
  "justify-center",
  "border-border",
  "text-foreground",
  "hover:bg-accent",
  "hover:text-accent-foreground",
  "data-[disabled]:opacity-50",
];

/** The decrement button sits left of the input, so it draws its border on the right. */
const DECREMENT_CLASSES = [...STEPPER_CLASSES, "border-r"];

/** The increment button sits right of the input, so it draws its border on the left. */
const INCREMENT_CLASSES = [...STEPPER_CLASSES, "border-l"];

/** The text control's utilities. */
const INPUT_CLASSES = [
  "h-9",
  "w-16",
  "bg-transparent",
  "px-2",
  "text-center",
  "text-sm",
  "text-foreground",
  "data-[disabled]:opacity-50",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/** Lets React apply the number field's ref-backed value commit. See note 2 above. */
function flush(): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, 0);
  });
}

interface RenderedNumberField {
  readonly container: HTMLElement;
  readonly root: HTMLElement;
  readonly scrubArea: HTMLElement;
  readonly group: HTMLElement;
  readonly decrement: HTMLButtonElement;
  readonly input: HTMLInputElement;
  readonly increment: HTMLButtonElement;
  /** Every part that mirrors the root's state, for the state-attribute sweeps. */
  readonly parts: readonly HTMLElement[];
}

async function renderNumberField(props: NumberFieldRootProps = {}): Promise<RenderedNumberField> {
  const { container } = await render(
    <NumberField.Root data-testid="nf-root" {...props}>
      <NumberField.ScrubArea data-testid="nf-scrub">
        <NumberField.ScrubAreaCursor data-testid="nf-cursor" />
      </NumberField.ScrubArea>
      <NumberField.Group data-testid="nf-group">
        <NumberField.Decrement data-testid="nf-dec">-</NumberField.Decrement>
        <NumberField.Input data-testid="nf-input" />
        <NumberField.Increment data-testid="nf-inc">+</NumberField.Increment>
      </NumberField.Group>
    </NumberField.Root>,
  );

  function part(testId: string): HTMLElement {
    const element = container.querySelector(`[data-testid="${testId}"]`);
    expect(element, testId).not.toBeNull();
    return element as HTMLElement;
  }

  const root = part("nf-root");
  const scrubArea = part("nf-scrub");
  const group = part("nf-group");
  const decrement = part("nf-dec") as HTMLButtonElement;
  const input = part("nf-input") as HTMLInputElement;
  const increment = part("nf-inc") as HTMLButtonElement;

  return {
    container,
    root,
    scrubArea,
    group,
    decrement,
    input,
    increment,
    parts: [root, scrubArea, group, decrement, input, increment],
  };
}

/** Starts a scrub and returns the portaled cursor, which is only mounted mid-scrub. */
function startScrub(scrubArea: HTMLElement): HTMLElement | null {
  scrubArea.dispatchEvent(
    new PointerEvent("pointerdown", {
      bubbles: true,
      button: 0,
      pointerType: "mouse",
      clientX: 100,
      clientY: 100,
    }),
  );

  return document.body.querySelector('[data-testid="nf-cursor"]');
}

/** Ends a scrub. The pointer is released on the document, not the scrub area. */
function endScrub(): void {
  document.dispatchEvent(new PointerEvent("pointerup", { bubbles: true, pointerType: "mouse" }));
}

describe("NumberField", () => {
  it("mirrors Base UI's part names 1:1", async () => {
    // The catalog's contract: a reader moves between Base UI's docs and this
    // component without a translation step. Compared against the real package
    // so a Base UI upgrade that adds a part fails here rather than silently
    // leaving the catalog a part short.
    expect(Object.keys(NumberField).toSorted()).toEqual(Object.keys(BaseNumberField).toSorted());
  });

  it("renders the root recipe on the wrapper", async () => {
    const { root } = await renderNumberField();

    expect(classSet(root)).toEqual(ROOT_CLASSES.toSorted());
  });

  it("renders the scrub-area recipe on the scrub area", async () => {
    const { scrubArea } = await renderNumberField();

    expect(classSet(scrubArea)).toEqual(SCRUB_AREA_CLASSES.toSorted());
  });

  it("renders the group recipe on the stepper shell", async () => {
    const { group } = await renderNumberField();

    expect(classSet(group)).toEqual(GROUP_CLASSES.toSorted());
  });

  it("renders the decrement recipe on the step-down button", async () => {
    const { decrement } = await renderNumberField();

    expect(classSet(decrement)).toEqual(DECREMENT_CLASSES.toSorted());
  });

  it("renders the input recipe on the text control", async () => {
    const { input } = await renderNumberField();

    expect(classSet(input)).toEqual(INPUT_CLASSES.toSorted());
  });

  it("renders the increment recipe on the step-up button", async () => {
    const { increment } = await renderNumberField();

    expect(classSet(increment)).toEqual(INCREMENT_CLASSES.toSorted());
  });

  it("renders the scrub-area-cursor recipe while scrubbing", async () => {
    // The only part that is NOT in the DOM at rest: Base UI mounts it into
    // document.body for the duration of a scrub and no longer. Asserted with no
    // await for the reason in note 3 at the top of this file.
    const { scrubArea } = await renderNumberField({ defaultValue: 0 });

    const cursor = startScrub(scrubArea);

    expect(cursor, "scrub cursor").not.toBeNull();
    expect(classSet(cursor as Element)).toEqual(SCRUB_AREA_CURSOR_CLASSES.toSorted());

    endScrub();
  });

  it("renders each part as the element Base UI documents", async () => {
    const { root, scrubArea, group, decrement, input, increment } = await renderNumberField();

    expect(root.tagName).toBe("DIV");
    expect(scrubArea.tagName).toBe("SPAN");
    expect(group.tagName).toBe("DIV");
    expect(group.getAttribute("role")).toBe("group");
    expect(decrement.tagName).toBe("BUTTON");
    expect(increment.tagName).toBe("BUTTON");
    expect(input.tagName).toBe("INPUT");
    // A text input with a numeric keypad, not type="number": Base UI parses and
    // formats the value itself, which type="number" would fight over.
    expect(input.type).toBe("text");
    expect(input.getAttribute("inputmode")).toBe("numeric");
  });

  it("wires the stepper buttons to the input for assistive tech", async () => {
    // The buttons are out of the tab order on purpose — a keyboard user steps
    // with the arrow keys on the input — so aria-controls is what still ties
    // them to the control they act on.
    const { decrement, input, increment } = await renderNumberField();

    expect(input.id).not.toBe("");
    expect(decrement.getAttribute("aria-controls")).toBe(input.id);
    expect(increment.getAttribute("aria-controls")).toBe(input.id);
    expect(decrement.getAttribute("aria-label")).toBe("Decrease");
    expect(increment.getAttribute("aria-label")).toBe("Increase");
    expect(decrement.tabIndex).toBe(-1);
    expect(increment.tabIndex).toBe(-1);
  });

  it("steps up and down by one step when the buttons are pressed", async () => {
    const onValueChange = vi.fn();
    const { input, increment, decrement } = await renderNumberField({
      defaultValue: 2,
      onValueChange,
    });

    increment.click();
    await flush();

    expect(input.value).toBe("3");
    expect(onValueChange.mock.calls[0]?.[0]).toBe(3);

    decrement.click();
    await flush();

    expect(input.value).toBe("2");
    expect(onValueChange.mock.calls[1]?.[0]).toBe(2);
  });

  it("steps by the configured step size", async () => {
    const { input, increment } = await renderNumberField({ defaultValue: 0, step: 5 });

    increment.click();
    await flush();

    expect(input.value).toBe("5");
  });

  it("steps from the keyboard on the focused input", async () => {
    // The arrow keys are the ONLY stepping route a keyboard user has, because
    // the buttons carry tabIndex -1.
    const { input } = await renderNumberField({ defaultValue: 2 });

    input.focus();
    await userEvent.keyboard("{ArrowUp}");

    expect(input.value).toBe("3");

    await userEvent.keyboard("{ArrowDown}{ArrowDown}");

    expect(input.value).toBe("1");
  });

  it("disables the increment button at max and the decrement button at min", async () => {
    // A boundary disables ONE button, not the field: `data-disabled` has to
    // land on the button that can no longer act while its twin stays live.
    const atMax = await renderNumberField({ defaultValue: 5, min: 0, max: 5 });

    expect(atMax.increment.disabled).toBe(true);
    expect(atMax.increment.hasAttribute("data-disabled")).toBe(true);
    expect(atMax.decrement.disabled).toBe(false);
    expect(atMax.decrement.hasAttribute("data-disabled")).toBe(false);
    expect(atMax.root.hasAttribute("data-disabled")).toBe(false);

    const atMin = await renderNumberField({ defaultValue: 0, min: 0, max: 5 });

    expect(atMin.decrement.disabled).toBe(true);
    expect(atMin.decrement.hasAttribute("data-disabled")).toBe(true);
    expect(atMin.increment.disabled).toBe(false);
  });

  it("exposes the disabled state on every part and refuses to step", async () => {
    const onValueChange = vi.fn();
    const { parts, input, increment } = await renderNumberField({
      defaultValue: 2,
      disabled: true,
      onValueChange,
    });

    for (const part of parts) {
      expect(part.hasAttribute("data-disabled"), part.dataset["testid"]).toBe(true);
    }
    expect(input.disabled).toBe(true);

    increment.click();
    await flush();

    expect(onValueChange).not.toHaveBeenCalled();
  });

  it("exposes the readOnly state on every part and refuses to step", async () => {
    const onValueChange = vi.fn();
    const { parts, input, increment } = await renderNumberField({
      defaultValue: 2,
      readOnly: true,
      onValueChange,
    });

    for (const part of parts) {
      expect(part.hasAttribute("data-readonly"), part.dataset["testid"]).toBe(true);
    }
    expect(input.readOnly).toBe(true);

    increment.click();
    await flush();

    expect(onValueChange).not.toHaveBeenCalled();
    expect(input.value).toBe("2");
  });

  it("exposes the required state on every part", async () => {
    const { parts, input } = await renderNumberField({ required: true });

    for (const part of parts) {
      expect(part.hasAttribute("data-required"), part.dataset["testid"]).toBe(true);
    }
    expect(input.required).toBe(true);
  });

  it("submits its value through a hidden number input", async () => {
    // The visible control is type="text" so Base UI can format it, which means
    // `name` has to reach a real numeric control for a form submission to carry
    // the value at all.
    const { container } = await renderNumberField({ defaultValue: 7, name: "quantity" });

    const hidden = container.querySelector('input[type="number"]');
    expect(hidden, "hidden input").not.toBeNull();
    expect((hidden as HTMLInputElement).name).toBe("quantity");
    expect((hidden as HTMLInputElement).value).toBe("7");
    expect(hidden?.getAttribute("aria-hidden")).toBe("true");
  });

  it("formats the displayed value without changing the numeric one", async () => {
    const onValueChange = vi.fn();
    const { input, increment } = await renderNumberField({
      defaultValue: 1234.5,
      format: { style: "currency", currency: "USD" },
      locale: "en-US",
      onValueChange,
    });

    expect(input.value).toBe("$1,234.50");

    increment.click();
    await flush();

    expect(onValueChange.mock.calls[0]?.[0]).toBe(1235.5);
    expect(input.value).toBe("$1,235.50");
  });

  it("publishes the scrubbing state on every part and clears it on release", async () => {
    const { parts, scrubArea } = await renderNumberField({ defaultValue: 0 });

    startScrub(scrubArea);

    for (const part of parts) {
      expect(part.hasAttribute("data-scrubbing"), part.dataset["testid"]).toBe(true);
    }

    endScrub();
    await flush();

    for (const part of parts) {
      expect(part.hasAttribute("data-scrubbing"), part.dataset["testid"]).toBe(false);
    }
  });

  it("leaves a controlled field for its owner to update", async () => {
    // `value` without a state update must NOT move: the component may not keep
    // private state behind the caller's back.
    const onValueChange = vi.fn();
    const { input, increment } = await renderNumberField({ value: 2, onValueChange });

    increment.click();
    await flush();

    expect(onValueChange.mock.calls[0]?.[0]).toBe(3);
    expect(input.value).toBe("2");
  });

  it("lets a caller className override a root recipe utility", async () => {
    // The cn()/tailwind-merge proof, asserted once per part below: the
    // conflicting utility is REMOVED rather than appended after, and the
    // utilities the caller never mentioned survive. A string-append
    // implementation leaves both on the element.
    const { root } = await renderNumberField({ className: "gap-4" });

    expect(root.classList.contains("gap-4")).toBe(true);
    expect(root.classList.contains("gap-1.5")).toBe(false);
    expect(root.classList.contains("flex-col")).toBe(true);
  });

  it("lets a caller className override a recipe utility on every other part", async () => {
    // Asserted per part rather than once: each part wires its own cn(), so a
    // component that merged only the root would still pass the test above.
    const { container } = await render(
      <NumberField.Root>
        <NumberField.ScrubArea className="cursor-ns-resize" data-testid="nf-scrub" />
        <NumberField.Group className="bg-muted" data-testid="nf-group">
          <NumberField.Decrement className="text-destructive" data-testid="nf-dec">
            -
          </NumberField.Decrement>
          <NumberField.Input className="w-24" data-testid="nf-input" />
          <NumberField.Increment className="size-11" data-testid="nf-inc">
            +
          </NumberField.Increment>
        </NumberField.Group>
      </NumberField.Root>,
    );

    function classes(testId: string): DOMTokenList {
      return (container.querySelector(`[data-testid="${testId}"]`) as HTMLElement).classList;
    }

    expect(classes("nf-scrub").contains("cursor-ns-resize")).toBe(true);
    expect(classes("nf-scrub").contains("cursor-ew-resize")).toBe(false);
    expect(classes("nf-scrub").contains("select-none")).toBe(true);

    expect(classes("nf-group").contains("bg-muted")).toBe(true);
    expect(classes("nf-group").contains("bg-background")).toBe(false);
    expect(classes("nf-group").contains("rounded-md")).toBe(true);

    // The hover colour survives: it is a different tailwind-merge group from
    // the base text colour the caller replaced.
    expect(classes("nf-dec").contains("text-destructive")).toBe(true);
    expect(classes("nf-dec").contains("text-foreground")).toBe(false);
    expect(classes("nf-dec").contains("hover:text-accent-foreground")).toBe(true);

    expect(classes("nf-input").contains("w-24")).toBe(true);
    expect(classes("nf-input").contains("w-16")).toBe(false);
    expect(classes("nf-input").contains("text-center")).toBe(true);

    expect(classes("nf-inc").contains("size-11")).toBe(true);
    expect(classes("nf-inc").contains("size-9")).toBe(false);
    expect(classes("nf-inc").contains("border-l")).toBe(true);
  });

  it("composes parts onto other elements through the render prop", async () => {
    const { container } = await render(
      <NumberField.Root render={<section />} data-testid="nf-root">
        <NumberField.Group render={<fieldset />} data-testid="nf-group">
          <NumberField.Input data-testid="nf-input" />
        </NumberField.Group>
      </NumberField.Root>,
    );

    const section = container.querySelector("section");
    const fieldset = container.querySelector("fieldset");
    expect(container.querySelector("div")).toBeNull();
    expect(classSet(section as Element)).toEqual(ROOT_CLASSES.toSorted());
    expect(classSet(fieldset as Element)).toEqual(GROUP_CLASSES.toSorted());
    expect(fieldset?.getAttribute("role")).toBe("group");
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(
      <NumberField.Root data-testid="checkout-quantity">
        <NumberField.Group>
          <NumberField.Input />
        </NumberField.Group>
      </NumberField.Root>,
    );

    expect(container.querySelector('[data-testid="checkout-quantity"]')).not.toBeNull();
  });
});
