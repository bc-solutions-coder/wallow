import { render } from "@bc-solutions-coder/testing/render";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";

import { OTPField, type OTPFieldRootProps } from "./otp-field";

/*
 * Follows the exemplar spec shape from Wallow-m5aq.2.1 (button.test.tsx):
 * browser project, nothing mocked, recipes asserted THROUGH the component, and
 * class assertions as an order-free SET so tailwind-merge may reorder.
 *
 * Two things about this component shape the spec:
 *
 * 1. `OTPField.Input` takes NO index prop. Each slot derives its index from its
 *    position among the root's slots, so "which slot got which character" is a
 *    real behaviour worth pinning rather than a prop being echoed back.
 * 2. `OTPField.Root` renders TWO things: the visible `<div role="group">` and a
 *    visually hidden `<input aria-hidden="true">` SIBLING carrying the whole
 *    code into form submission. Every helper below therefore separates the slot
 *    inputs from that hidden one; querying `container` for `input` blindly
 *    would silently pick it up.
 *
 * Interaction is driven with `element.focus()` plus the real `userEvent.
 * keyboard`, never `userEvent.click`: the vitest browser project loads no
 * Tailwind, so a recipe-sized element has no box for Playwright's actionability
 * check and a click hangs for the full timeout instead of failing. Slot inputs
 * happen to have an intrinsic box, but the rule is kept uniform across the
 * catalog. The *visual* half — the slot row's spacing, the 40px slots, the
 * filled-slot border — belongs to `otp-field.stories.tsx`, which renders under
 * the real Tailwind pipeline.
 */

/** The slot row's utilities. Single source of truth for the class assertions. */
const ROOT_CLASSES = ["flex", "items-center", "gap-2", "data-[disabled]:opacity-50"];

/**
 * One slot's utilities. `data-[filled]:border-primary` is the only state
 * treatment here, and it is deliberately NOT `data-[focused]:`: Base UI stamps
 * `data-focused` on EVERY slot when any one of them is focused, so a focus
 * treatment written that way would light up the whole row (verified against the
 * real part). `data-filled`, by contrast, is genuinely per-slot.
 */
const INPUT_CLASSES = [
  "size-10",
  "shrink-0",
  "rounded-md",
  "border",
  "border-border",
  "bg-background",
  "text-center",
  "text-sm",
  "text-foreground",
  "data-[filled]:border-primary",
];

/**
 * The separator's utilities. Its two sizes hang off Base UI's own
 * `data-orientation` rather than a cva variant, so the caller sets the
 * orientation once — on the part, where it also drives `aria-orientation` —
 * instead of having to keep a style prop in step with it.
 */
const SEPARATOR_CLASSES = [
  "shrink-0",
  "bg-border",
  "data-[orientation=horizontal]:h-px",
  "data-[orientation=horizontal]:w-2",
  "data-[orientation=vertical]:h-4",
  "data-[orientation=vertical]:w-px",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

interface RenderedOTPField {
  readonly container: HTMLElement;
  /** The visible `<div role="group">`. */
  readonly root: HTMLElement;
  /** The character slots, in DOM order. */
  readonly slots: HTMLInputElement[];
  /** The visually hidden input Base UI submits with the form. */
  readonly hidden: HTMLInputElement;
}

/** Renders `length` slots and nothing else. */
async function renderOTPField(
  props: Omit<OTPFieldRootProps, "length"> & { readonly length?: number } = {},
): Promise<RenderedOTPField> {
  const { length = 3, ...rest } = props;
  const { container } = await render(
    <OTPField.Root length={length} id="otp" {...rest}>
      {Array.from({ length }, (_, index) => (
        <OTPField.Input key={index} />
      ))}
    </OTPField.Root>,
  );

  return collect(container, length);
}

function collect(container: HTMLElement, length: number): RenderedOTPField {
  const root = container.querySelector('[role="group"]');
  const hidden = container.querySelector('input[aria-hidden="true"]');
  expect(root, "root").not.toBeNull();
  expect(hidden, "hidden input").not.toBeNull();

  const slots = [...(root as HTMLElement).querySelectorAll("input")];
  expect(slots, "slot count").toHaveLength(length);

  return {
    container,
    root: root as HTMLElement,
    slots,
    hidden: hidden as HTMLInputElement,
  };
}

describe("OTPField", () => {
  it("renders the row recipe on the group element", async () => {
    const { root } = await renderOTPField();

    expect(classSet(root)).toEqual(ROOT_CLASSES.toSorted());
  });

  it("renders the slot recipe on every slot", async () => {
    // Every slot renders through the same part with no index prop, so a
    // per-slot assertion is what catches a recipe applied only to the first.
    const { slots } = await renderOTPField({ length: 4 });

    for (const slot of slots) {
      expect(classSet(slot)).toEqual(INPUT_CLASSES.toSorted());
    }
  });

  it("renders the separator recipe on the separator element", async () => {
    const { container } = await render(
      <OTPField.Root length={2} id="otp">
        <OTPField.Input />
        <OTPField.Separator />
        <OTPField.Input />
      </OTPField.Root>,
    );

    const separator = container.querySelector('[role="separator"]');
    expect(separator, "separator").not.toBeNull();
    expect((separator as HTMLElement).getAttribute("data-orientation")).toBe("horizontal");
    expect(classSet(separator as Element)).toEqual(SEPARATOR_CLASSES.toSorted());
  });

  it("numbers the slots from the root id, and a separator does not consume a number", async () => {
    // The slot's index comes from its position among the root's slots, not from
    // a prop — so a decorative part sitting between two slots must not shift
    // the ones after it.
    const { container } = await render(
      <OTPField.Root length={4} id="otp">
        <OTPField.Input />
        <OTPField.Input />
        <OTPField.Separator />
        <OTPField.Input />
        <OTPField.Input />
      </OTPField.Root>,
    );

    const { slots } = collect(container, 4);
    expect(slots.map((slot) => slot.id)).toEqual(["otp", "otp-2", "otp-3", "otp-4"]);
  });

  it("spreads a defaultValue across the slots and marks only the filled ones", async () => {
    const { root, slots, hidden } = await renderOTPField({ defaultValue: "12" });

    expect(slots.map((slot) => slot.value)).toEqual(["1", "2", ""]);
    expect(hidden.value).toBe("12");
    expect(root.hasAttribute("data-filled")).toBe(true);
    expect(root.hasAttribute("data-complete")).toBe(false);
    expect(slots.map((slot) => slot.hasAttribute("data-filled"))).toEqual([true, true, false]);
    // The state the slot recipe actually styles: a filled slot must carry both
    // the attribute and the rule that reacts to it.
    expect(slots[0]?.classList.contains("data-[filled]:border-primary")).toBe(true);
  });

  it("marks the row and every slot complete once the last slot is filled", async () => {
    const { root, slots } = await renderOTPField({ defaultValue: "123" });

    expect(root.hasAttribute("data-complete")).toBe(true);
    for (const slot of slots) {
      expect(slot.hasAttribute("data-complete")).toBe(true);
    }
  });

  it("advances to the next slot as the user types, reporting each value", async () => {
    const onValueChange = vi.fn();
    const { slots } = await renderOTPField({ onValueChange });

    slots[0]?.focus();
    await userEvent.keyboard("1");

    expect(document.activeElement).toBe(slots[1]);
    expect(onValueChange).toHaveBeenCalledTimes(1);
    expect(onValueChange.mock.calls[0]?.[0]).toBe("1");

    await userEvent.keyboard("2");

    expect(document.activeElement).toBe(slots[2]);
    expect(onValueChange.mock.calls[1]?.[0]).toBe("12");
  });

  it("announces the finished code once through onValueComplete", async () => {
    const onValueComplete = vi.fn();
    const { root, slots, hidden } = await renderOTPField({ onValueComplete });

    slots[0]?.focus();
    await userEvent.keyboard("123");

    expect(onValueComplete).toHaveBeenCalledTimes(1);
    expect(onValueComplete.mock.calls[0]?.[0]).toBe("123");
    expect(root.hasAttribute("data-complete")).toBe(true);
    expect(hidden.value).toBe("123");
  });

  it("drops characters the validation type rejects and reports them", async () => {
    // The default validationType is 'numeric', so a letter never reaches the
    // value — and the rejection is surfaced rather than swallowed.
    const onValueInvalid = vi.fn();
    const { slots } = await renderOTPField({ onValueInvalid });

    slots[0]?.focus();
    await userEvent.keyboard("1a23");

    expect(onValueInvalid).toHaveBeenCalledTimes(1);
    expect(onValueInvalid.mock.calls[0]?.[0]).toBe("a");
    expect(slots.map((slot) => slot.value)).toEqual(["1", "2", "3"]);
  });

  it("walks between slots with the arrow keys", async () => {
    // The slots are separate inputs with a roving tabindex, so lateral movement
    // is Base UI's own keyboard handling, not the browser's.
    const { slots } = await renderOTPField({ defaultValue: "12" });

    slots[0]?.focus();
    await userEvent.keyboard("{ArrowRight}");

    expect(document.activeElement).toBe(slots[1]);

    await userEvent.keyboard("{ArrowLeft}");

    expect(document.activeElement).toBe(slots[0]);
  });

  it("leaves a controlled value for its owner to update", async () => {
    // `value` without a state update must NOT move: the component may not keep
    // private state behind the caller's back.
    const onValueChange = vi.fn();
    const { root, slots, hidden } = await renderOTPField({ value: "12", onValueChange });

    slots[2]?.focus();
    await userEvent.keyboard("3");

    expect(onValueChange.mock.calls[0]?.[0]).toBe("123");
    expect(slots.map((slot) => slot.value)).toEqual(["1", "2", ""]);
    expect(hidden.value).toBe("12");
    expect(root.hasAttribute("data-complete")).toBe(false);
  });

  it("exposes the disabled state on the row and every slot, and refuses input", async () => {
    const onValueChange = vi.fn();
    const { root, slots } = await renderOTPField({ disabled: true, onValueChange });

    expect(root.hasAttribute("data-disabled")).toBe(true);
    expect(root.classList.contains("data-[disabled]:opacity-50")).toBe(true);
    for (const slot of slots) {
      expect(slot.hasAttribute("data-disabled")).toBe(true);
      expect(slot.disabled).toBe(true);
    }

    slots[0]?.focus();
    await userEvent.keyboard("1");

    expect(onValueChange).not.toHaveBeenCalled();
  });

  it("exposes the readOnly state on the row and every slot, and refuses input", async () => {
    // Unlike disabled, a read-only field still takes focus — it is readable and
    // copyable, just not editable.
    const onValueChange = vi.fn();
    const { root, slots } = await renderOTPField({ readOnly: true, onValueChange });

    expect(root.hasAttribute("data-readonly")).toBe(true);
    for (const slot of slots) {
      expect(slot.hasAttribute("data-readonly")).toBe(true);
      expect(slot.readOnly).toBe(true);
    }

    slots[0]?.focus();
    expect(document.activeElement).toBe(slots[0]);
    await userEvent.keyboard("1");

    expect(onValueChange).not.toHaveBeenCalled();
    expect(slots.map((slot) => slot.value)).toEqual(["", "", ""]);
  });

  it("exposes the required state on the row, every slot and the hidden input", async () => {
    const { root, slots, hidden } = await renderOTPField({ required: true });

    expect(root.hasAttribute("data-required")).toBe(true);
    expect(hidden.required).toBe(true);
    for (const slot of slots) {
      expect(slot.hasAttribute("data-required")).toBe(true);
      expect(slot.required).toBe(true);
    }
  });

  it("masks the slots without masking the submitted value", async () => {
    const { slots, hidden } = await renderOTPField({ mask: true, defaultValue: "123" });

    for (const slot of slots) {
      expect(slot.type).toBe("password");
    }
    expect(hidden.type).toBe("text");
    expect(hidden.value).toBe("123");
  });

  it("submits the whole code through the hidden input", async () => {
    // The reason an OTP field is form-usable at all: the value the user sees is
    // spread across N separate inputs, so `name` has to reach one real control
    // carrying the joined code.
    const { hidden } = await renderOTPField({ name: "otp-code", defaultValue: "123" });

    expect(hidden.name).toBe("otp-code");
    expect(hidden.value).toBe("123");
    expect(hidden.getAttribute("tabindex")).toBe("-1");
  });

  it("lets a caller className override a row recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting utility is REMOVED rather
    // than appended after, and the utilities the caller never mentioned — the
    // layout and the disabled treatment — survive. A string-append
    // implementation leaves both `gap-2` and `gap-4` on the element.
    const { root } = await renderOTPField({ className: "gap-4" });

    expect(root.classList.contains("gap-4")).toBe(true);
    expect(root.classList.contains("gap-2")).toBe(false);
    expect(root.classList.contains("flex")).toBe(true);
    expect(root.classList.contains("data-[disabled]:opacity-50")).toBe(true);
  });

  it("lets a caller className override a slot recipe utility", async () => {
    // Asserted separately from the row: each part wires its own cn(), and a
    // component that merged only the root would still pass the test above.
    const { container } = await render(
      <OTPField.Root length={2} id="otp">
        <OTPField.Input className="bg-accent" />
        <OTPField.Input />
      </OTPField.Root>,
    );

    const { slots } = collect(container, 2);
    expect(slots[0]?.classList.contains("bg-accent")).toBe(true);
    expect(slots[0]?.classList.contains("bg-background")).toBe(false);
    expect(slots[0]?.classList.contains("size-10")).toBe(true);
    expect(slots[0]?.classList.contains("data-[filled]:border-primary")).toBe(true);
    // The sibling slot is untouched: the override is per-part, not per-row.
    expect(slots[1]?.classList.contains("bg-background")).toBe(true);
  });

  it("lets a caller className override a separator recipe utility", async () => {
    const { container } = await render(
      <OTPField.Root length={2} id="otp">
        <OTPField.Input />
        <OTPField.Separator className="bg-accent" />
        <OTPField.Input />
      </OTPField.Root>,
    );

    const separator = container.querySelector('[role="separator"]') as HTMLElement;
    expect(separator.classList.contains("bg-accent")).toBe(true);
    expect(separator.classList.contains("bg-border")).toBe(false);
    expect(separator.classList.contains("shrink-0")).toBe(true);
  });

  it("composes the row onto another element through the render prop", async () => {
    const { container } = await render(
      <OTPField.Root length={2} id="otp" render={<section />}>
        <OTPField.Input />
        <OTPField.Input />
      </OTPField.Root>,
    );

    const section = container.querySelector("section");
    expect(section?.getAttribute("role")).toBe("group");
    expect(classSet(section as Element)).toEqual(ROOT_CLASSES.toSorted());
    expect(container.querySelector('div[role="group"]')).toBeNull();
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(
      <OTPField.Root length={2} id="otp" data-testid="mfa-code">
        <OTPField.Input data-testid="mfa-code-slot" />
        <OTPField.Input />
      </OTPField.Root>,
    );

    expect(container.querySelector('[data-testid="mfa-code"]')).not.toBeNull();
    expect(container.querySelector('[data-testid="mfa-code-slot"]')).not.toBeNull();
  });
});
