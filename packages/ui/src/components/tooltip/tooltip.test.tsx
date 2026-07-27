import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Tooltip, type TooltipProviderProps } from "./tooltip";

/*
 * Tooltip behavioural spec (Wallow-m5aq.3.4), shaped after the Wallow-m5aq.3.1
 * Dialog exemplar:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `tooltipPopupRecipe` and inspecting its return value: a recipe unit test
 *      would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into tooltip.styles.ts.
 *   4. Stories carry the visual coverage (see tooltip.stories.tsx); this file is
 *      only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <button data-base-ui-tooltip-trigger id>                 <- Tooltip.Trigger
 *     …gains data-popup-open while open; gains data-trigger-disabled and LOSES
 *     the data-base-ui-tooltip-trigger identifier while disabled
 *
 *   …and, only while open, portalled onto <body>:
 *   <div data-base-ui-portal>                                <- Tooltip.Portal
 *     <div role="presentation" data-open data-side data-align
 *          style="position:absolute;left;top;transform;--available-*;--anchor-*">
 *                                                            <- Tooltip.Positioner
 *       <div data-open data-side data-align tabindex="-1" data-base-ui-focusable>
 *                                                            <- Tooltip.Popup
 *         <div aria-hidden data-open style="position:absolute;left">
 *                                                            <- Tooltip.Arrow
 *         <div>                                              <- Tooltip.Viewport
 *
 * Seven consequences worth knowing before editing this file, the first four
 * inherited from the Dialog exemplar and the last three specific to a tooltip:
 *
 *   - the whole open half is PORTALLED to <body>, so every open-state query goes
 *     through `document.body`, never through `render`'s `container`;
 *   - nothing under Tooltip.Portal exists in the DOM at all while the tooltip is
 *     closed — these are not hidden elements, they are absent ones;
 *   - CLOSING IS ANIMATION-FRAME-DEFERRED. Base UI gates the unmount behind
 *     `useOpenChangeComplete` -> `useAnimationsFinished`, so the popup is still
 *     in the DOM for at least one rAF after the close resolves (measured: still
 *     present synchronously after a controlled `open` flips to false). Every
 *     absence assertion uses `await expect.poll(...)`, never a bare synchronous
 *     `expect(...).toBeNull()`;
 *   - `data-starting-style` / `data-ending-style` only exist DURING a transition,
 *     so no spec here asserts them on an element. They are pinned as
 *     `data-[starting-style]:` / `data-[ending-style]:` modifiers inside the
 *     recipe class sets instead, which is what the catalog actually owns;
 *   - A TOOLTIP HAS NO POINTER BLOCKER, BUT IT DOES DISMISS ON OUTSIDE PRESS.
 *     Unlike a modal dialog, Base UI renders NOTHING inside the portal but the
 *     positioner (measured: one child), so `userEvent.click` inside an open
 *     popup lands rather than timing out. The missing blocker is easy to
 *     misread as "no outside-press dismissal either" — it is not: a press
 *     anywhere outside closes the tooltip and reports
 *     `reason: "outside-press"` (measured). This bites hardest in a CONTROLLED
 *     tooltip driven from a button of the caller's own: that button's click
 *     opens the tooltip and then dismisses it again within the same gesture, so
 *     an external control can close a tooltip but cannot open one;
 *   - BASE UI WIRES NO ARIA ON A TOOLTIP (measured, and confirmed by grepping
 *     the package: the only `aria-*` in `@base-ui/react/tooltip` is the arrow's
 *     `aria-hidden`). There is no `role="tooltip"`, no `aria-describedby`, no
 *     `aria-expanded` and no `aria-controls`. The anatomy spec below PINS that,
 *     so a future Base UI release adding the wiring shows up as a failure rather
 *     than a silent behaviour change;
 *   - OPENING IS FOCUS-DRIVEN HERE. `trigger.focus()` opens the tooltip
 *     immediately and marks it `data-instant="focus"`, whereas hovering waits
 *     out Base UI's 600 ms default delay and needs the real Playwright mouse.
 *     Every spec below opens through focus, with ONE exception: the last spec in
 *     the file proves `Tooltip.Provider`'s `delay` actually reaches Base UI, and
 *     that can only be shown by hovering. It is LAST deliberately — the real
 *     mouse position persists between specs in a file, and a trigger left under
 *     the pointer opens on render, which would turn a later focus spec's open
 *     into a hover-driven one.
 */

/** Utilities `Tooltip.Trigger` must render. Deliberately colourless, for the same
 * reason as the dialog trigger: a tooltip trigger is routinely composed onto a
 * real `Button` through Base UI's `render` prop, and a background here would be
 * merged away by tailwind-merge and silently beat the Button's own. Note the
 * disabled modifier is `data-[trigger-disabled]:`, NOT `data-[disabled]:` —
 * measured: Base UI stamps `data-trigger-disabled` on a tooltip trigger. */
const TRIGGER_CLASSES = [
  "inline-flex",
  "items-center",
  "justify-center",
  "rounded-md",
  "text-sm",
  "font-medium",
  "transition-colors",
  "data-[trigger-disabled]:opacity-50",
];

/**
 * Utilities `Tooltip.Positioner` must render. Base UI owns this element's
 * `position`, `left`, `top` and `transform` as INLINE styles, so the recipe may
 * only add stacking and focus concerns — never layout that would fight the
 * positioning engine. This is `Select.Positioner`'s rule, not `Dialog.Popup`'s:
 * do NOT copy the dialog's centring utilities onto an anchored overlay.
 */
const POSITIONER_CLASSES = ["z-50", "outline-none"];

/**
 * Utilities `Tooltip.Popup` must render — the bubble itself. Smaller and lighter
 * than the dialog's popup (`text-xs`, `px-3 py-1.5`, `shadow-md`) because a
 * tooltip is a label, not a surface, and it carries NO positioning: the
 * positioner above already placed it.
 */
const POPUP_CLASSES = [
  "rounded-md",
  "border",
  "border-border",
  "bg-popover",
  "px-3",
  "py-1.5",
  "text-xs",
  "text-popover-foreground",
  "shadow-md",
  "transition-all",
  "duration-150",
  "data-[starting-style]:scale-95",
  "data-[starting-style]:opacity-0",
  "data-[ending-style]:scale-95",
  "data-[ending-style]:opacity-0",
];

/**
 * Utilities `Tooltip.Arrow` must render. Base UI sets the arrow's `position` and
 * offset inline exactly as it does for the positioner, so this recipe adds
 * colour and layout for the caller's glyph only.
 */
const ARROW_CLASSES = ["flex", "text-popover-foreground"];

/**
 * Utilities `Tooltip.Viewport` must render. The viewport crossfades the popup's
 * contents when one popup is shared by several triggers, so it needs a
 * positioning context for the outgoing copy and clipping while the two overlap —
 * and nothing else, since it must not impose a box on a popup that has no
 * viewport at all.
 */
const VIEWPORT_CLASSES = ["relative", "overflow-hidden"];

/**
 * Every member `@base-ui/react/tooltip` publishes on its namespace, sorted.
 * `Handle` and `createHandle` are the imperative API for detached triggers; they
 * are re-exported unwrapped rather than dropped, so this catalog's namespace
 * keys still mirror Base UI's 1:1.
 */
const BASE_UI_PART_NAMES = [
  "Arrow",
  "Handle",
  "Popup",
  "Portal",
  "Positioner",
  "Provider",
  "Root",
  "Trigger",
  "Viewport",
  "createHandle",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/**
 * The part carrying `data-testid`, searched across the whole document because
 * the open half of a tooltip is portalled out of the render container.
 */
function part(testId: string): HTMLElement {
  const element = document.body.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
  expect(element, `no element with data-testid="${testId}"`).not.toBeNull();
  return element as HTMLElement;
}

/** The same lookup for parts that are legitimately absent. */
function maybePart(testId: string): HTMLElement | null {
  return document.body.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
}

/**
 * Every part at once, so one fixture can carry the whole anatomy.
 *
 * `defaultOpen` is the fixture's own prop rather than a hard-coded value because
 * the structural and recipe specs want the tooltip already open with no
 * interaction at all, while the open/close specs want to drive it themselves.
 */
function FullTooltip({ defaultOpen = false }: { readonly defaultOpen?: boolean }): ReactElement {
  return (
    <Tooltip.Root defaultOpen={defaultOpen}>
      <Tooltip.Trigger data-testid="t-trigger">Save</Tooltip.Trigger>
      <Tooltip.Portal data-testid="t-portal">
        <Tooltip.Positioner data-testid="t-positioner">
          <Tooltip.Popup data-testid="t-popup">
            <Tooltip.Arrow data-testid="t-arrow" />
            <Tooltip.Viewport data-testid="t-viewport">Saves your work</Tooltip.Viewport>
          </Tooltip.Popup>
        </Tooltip.Positioner>
      </Tooltip.Portal>
    </Tooltip.Root>
  );
}

/**
 * Renders the full fixture and opens it by focusing the trigger.
 *
 * Focus rather than hover is load-bearing: it opens with no delay to wait out
 * and no real pointer to place, and Base UI marks the result
 * `data-instant="focus"` (measured), which is the state this spec asserts.
 */
async function openTooltip(): Promise<void> {
  await render(<FullTooltip />);

  part("t-trigger").focus();
  await expect.poll(() => maybePart("t-popup")).not.toBeNull();
}

describe("Tooltip", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A key added here
    // that Base UI does not have (or a missing one) fails.
    expect(Object.keys(Tooltip).toSorted()).toEqual(BASE_UI_PART_NAMES);
  });

  it("renders the trigger as a button Base UI has claimed, with no aria wiring", async () => {
    // PINS a measured Base UI 1.6.0 behaviour rather than an aspiration: a
    // tooltip gets no `aria-describedby`, no `aria-expanded` and no
    // `aria-controls`, so the catalog documents the tooltip as supplementary
    // rather than pretending the popup is announced. If a later Base UI adds the
    // wiring, this fails and the decision gets revisited deliberately.
    await render(<FullTooltip />);

    const trigger = part("t-trigger");
    expect(trigger.tagName).toBe("BUTTON");
    expect(trigger.hasAttribute("data-base-ui-tooltip-trigger")).toBe(true);
    expect(trigger.hasAttribute("data-popup-open")).toBe(false);
    expect(trigger.getAttribute("aria-describedby")).toBeNull();
    expect(trigger.getAttribute("aria-expanded")).toBeNull();
    expect(trigger.getAttribute("aria-controls")).toBeNull();
  });

  it("keeps every portalled part out of the DOM while closed", async () => {
    // Base UI's default: these are absent elements, not hidden ones, so no
    // recipe can be asserted on them until the tooltip opens.
    await render(<FullTooltip />);

    expect(maybePart("t-portal")).toBeNull();
    expect(maybePart("t-positioner")).toBeNull();
    expect(maybePart("t-popup")).toBeNull();
    expect(maybePart("t-arrow")).toBeNull();
    expect(maybePart("t-viewport")).toBeNull();
  });

  it("anchors the popup through the positioner, which owns the inline layout", async () => {
    // The structural fact every recipe below depends on, asserted with no
    // interaction at all. Base UI writes `position`/`left`/`top` onto the
    // POSITIONER; the popup gets no positioning of its own, which is why the
    // popup recipe carries none either.
    await render(<FullTooltip defaultOpen />);

    const positioner = part("t-positioner");
    expect(positioner.getAttribute("role")).toBe("presentation");
    expect(positioner.style.position).toBe("absolute");
    expect(positioner.hasAttribute("data-open")).toBe(true);
    expect(positioner.hasAttribute("data-side")).toBe(true);
    expect(positioner.hasAttribute("data-align")).toBe(true);

    const popup = part("t-popup");
    expect(positioner.contains(popup)).toBe(true);
    expect(popup.style.position).toBe("");
    expect(popup.hasAttribute("data-open")).toBe(true);
    expect(popup.getAttribute("role")).toBeNull();

    // The arrow is decorative and positioned inline by Base UI too.
    expect(part("t-arrow").getAttribute("aria-hidden")).toBe("true");
    expect(part("t-arrow").style.position).toBe("absolute");
  });

  it("opens on trigger focus and marks the transition instant", async () => {
    await openTooltip();

    expect(part("t-trigger").hasAttribute("data-popup-open")).toBe(true);
    expect(part("t-popup").hasAttribute("data-open")).toBe(true);
    // Base UI skips the enter animation for a focus-driven open; the catalog
    // relies on this, because it is what makes focus a stable way to open a
    // tooltip in a test without waiting out a transition.
    expect(part("t-popup").getAttribute("data-instant")).toBe("focus");
  });

  it("closes and unmounts the popup when focus leaves the trigger", async () => {
    await render(
      <>
        <FullTooltip />
        <button type="button" data-testid="t-elsewhere">
          Elsewhere
        </button>
      </>,
    );

    part("t-trigger").focus();
    await expect.poll(() => maybePart("t-popup")).not.toBeNull();

    part("t-elsewhere").focus();

    // Polled, not read once: the unmount is gated behind an animation frame.
    await expect.poll(() => maybePart("t-popup")).toBeNull();
    expect(part("t-trigger").hasAttribute("data-popup-open")).toBe(false);
  });

  it("closes and unmounts the popup on Escape", async () => {
    await openTooltip();

    await userEvent.keyboard("{Escape}");

    await expect.poll(() => maybePart("t-popup")).toBeNull();
    expect(maybePart("t-positioner")).toBeNull();
  });

  it("renders the trigger with its recipe", async () => {
    await render(<FullTooltip />);

    expect(classSet(part("t-trigger"))).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("renders the positioner and popup with their recipes", async () => {
    await render(<FullTooltip defaultOpen />);

    expect(classSet(part("t-positioner"))).toEqual(POSITIONER_CLASSES.toSorted());
    expect(classSet(part("t-popup"))).toEqual(POPUP_CLASSES.toSorted());
  });

  it("renders the arrow and viewport with their recipes", async () => {
    await render(<FullTooltip defaultOpen />);

    expect(classSet(part("t-arrow"))).toEqual(ARROW_CLASSES.toSorted());
    expect(classSet(part("t-viewport"))).toEqual(VIEWPORT_CLASSES.toSorted());
  });

  it("marks a disabled trigger and never opens for it", async () => {
    // `data-trigger-disabled` is the attribute the trigger recipe's only
    // modifier keys off, and Base UI also drops its own trigger identifier while
    // disabled — both measured, both pinned here so the recipe's modifier cannot
    // quietly stop matching.
    await render(
      <Tooltip.Root disabled defaultOpen>
        <Tooltip.Trigger data-testid="x-trigger">Save</Tooltip.Trigger>
        <Tooltip.Portal>
          <Tooltip.Positioner>
            <Tooltip.Popup data-testid="x-popup">Saves your work</Tooltip.Popup>
          </Tooltip.Positioner>
        </Tooltip.Portal>
      </Tooltip.Root>,
    );

    const trigger = part("x-trigger");
    expect(trigger.hasAttribute("data-trigger-disabled")).toBe(true);
    expect(trigger.hasAttribute("data-base-ui-tooltip-trigger")).toBe(false);
    expect(maybePart("x-popup")).toBeNull();
  });

  it("closes when a press lands outside the popup", async () => {
    // Pressing outside dismisses a tooltip even though nothing blocks the
    // pointer. The press is DISPATCHED rather than driven with
    // `userEvent.click`, so it costs no real mouse movement: the Playwright
    // pointer position survives between specs in a file, and parking it over a
    // trigger reopens that tooltip on the next render.
    const onOpenChange = vi.fn();
    await render(
      <Tooltip.Root defaultOpen onOpenChange={onOpenChange}>
        <Tooltip.Trigger data-testid="p-trigger">Save</Tooltip.Trigger>
        <Tooltip.Portal>
          <Tooltip.Positioner>
            <Tooltip.Popup data-testid="p-popup">Saves your work</Tooltip.Popup>
          </Tooltip.Positioner>
        </Tooltip.Portal>
      </Tooltip.Root>,
    );
    expect(maybePart("p-popup")).not.toBeNull();

    document.body.dispatchEvent(new PointerEvent("pointerdown", { bubbles: true, composed: true }));
    document.body.dispatchEvent(new MouseEvent("click", { bubbles: true, composed: true }));

    await expect.poll(() => maybePart("p-popup")).toBeNull();
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(false);
    expect((onOpenChange.mock.calls[0]?.[1] as { reason?: string })?.reason).toBe("outside-press");
  });

  it("reports open state to onOpenChange with Base UI's event details", async () => {
    // The caller's handler has to survive Base UI's own mergeProps.
    const onOpenChange = vi.fn();
    await render(
      <Tooltip.Root onOpenChange={onOpenChange}>
        <Tooltip.Trigger data-testid="o-trigger">Save</Tooltip.Trigger>
        <Tooltip.Portal>
          <Tooltip.Positioner>
            <Tooltip.Popup data-testid="o-popup">Saves your work</Tooltip.Popup>
          </Tooltip.Positioner>
        </Tooltip.Portal>
      </Tooltip.Root>,
    );

    part("o-trigger").focus();
    await expect.poll(() => maybePart("o-popup")).not.toBeNull();

    expect(onOpenChange).toHaveBeenCalledTimes(1);
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);
    expect(onOpenChange.mock.calls[0]?.[1]).toHaveProperty("reason");
  });

  it("honours a controlled open prop", async () => {
    const controlled = (open: boolean): ReactElement => (
      <Tooltip.Root open={open}>
        <Tooltip.Trigger data-testid="c-trigger">Save</Tooltip.Trigger>
        <Tooltip.Portal>
          <Tooltip.Positioner>
            <Tooltip.Popup data-testid="c-popup">Saves your work</Tooltip.Popup>
          </Tooltip.Positioner>
        </Tooltip.Portal>
      </Tooltip.Root>
    );

    const { rerender } = await render(controlled(false));
    expect(maybePart("c-popup")).toBeNull();

    await rerender(controlled(true));
    expect(part("c-popup").hasAttribute("data-open")).toBe(true);
    expect(part("c-trigger").hasAttribute("data-popup-open")).toBe(true);

    await rerender(controlled(false));
    await expect.poll(() => maybePart("c-popup")).toBeNull();
  });

  it("lets a caller className override popup and positioner recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both background classes on and fails.
    await render(
      <Tooltip.Root defaultOpen>
        <Tooltip.Trigger data-testid="v-trigger">Save</Tooltip.Trigger>
        <Tooltip.Portal>
          <Tooltip.Positioner data-testid="v-positioner" className="z-10">
            <Tooltip.Popup data-testid="v-popup" className="bg-accent text-sm">
              Saves your work
            </Tooltip.Popup>
          </Tooltip.Positioner>
        </Tooltip.Portal>
      </Tooltip.Root>,
    );

    const positioner = part("v-positioner");
    expect(positioner.classList.contains("z-10")).toBe(true);
    expect(positioner.classList.contains("z-50")).toBe(false);
    expect(positioner.classList.contains("outline-none")).toBe(true);

    const popup = part("v-popup");
    expect(popup.classList.contains("bg-accent")).toBe(true);
    expect(popup.classList.contains("bg-popover")).toBe(false);
    expect(popup.classList.contains("text-sm")).toBe(true);
    expect(popup.classList.contains("text-xs")).toBe(false);
    expect(popup.classList.contains("border-border")).toBe(true);
    expect(popup.classList.contains("data-[ending-style]:scale-95")).toBe(true);
  });

  it("carries the popup recipe onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes.
    await render(
      <Tooltip.Root defaultOpen>
        <Tooltip.Trigger data-testid="r-trigger">Save</Tooltip.Trigger>
        <Tooltip.Portal>
          <Tooltip.Positioner>
            <Tooltip.Popup data-testid="r-popup" render={<section />}>
              Saves your work
            </Tooltip.Popup>
          </Tooltip.Positioner>
        </Tooltip.Portal>
      </Tooltip.Root>,
    );

    const popup = part("r-popup");
    expect(popup.tagName).toBe("SECTION");
    expect(classSet(popup)).toEqual(POPUP_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <Tooltip.Root defaultOpen>
        <Tooltip.Trigger data-testid="save-draft" aria-label="Save draft">
          Save
        </Tooltip.Trigger>
        <Tooltip.Portal>
          <Tooltip.Positioner>
            <Tooltip.Popup data-testid="save-draft-tip" lang="en">
              Saves your work
            </Tooltip.Popup>
          </Tooltip.Positioner>
        </Tooltip.Portal>
      </Tooltip.Root>,
    );

    expect(part("save-draft").getAttribute("aria-label")).toBe("Save draft");
    expect(part("save-draft-tip").getAttribute("lang")).toBe("en");
  });

  it("composes a Provider around the Root without rendering an element", async () => {
    // The delay props are held in a typed object rather than inline literals so
    // this spec also pins `TooltipProviderProps` — a fork keeping one delay
    // policy in configuration is the reason the type is exported at all.
    const delays: TooltipProviderProps = { delay: 0, closeDelay: 0, timeout: 400 };

    const { container } = await render(
      <Tooltip.Provider {...delays}>
        <Tooltip.Root>
          <Tooltip.Trigger data-testid="g-trigger">Save</Tooltip.Trigger>
          <Tooltip.Portal>
            <Tooltip.Positioner>
              <Tooltip.Popup data-testid="g-popup">Saves your work</Tooltip.Popup>
            </Tooltip.Positioner>
          </Tooltip.Portal>
        </Tooltip.Root>
      </Tooltip.Provider>,
    );

    // The provider adds no wrapper element: the trigger is the container's only
    // child, exactly as it is without one.
    expect(container.children.length).toBe(1);
    expect(container.firstElementChild).toBe(part("g-trigger"));

    part("g-trigger").focus();
    await expect.poll(() => maybePart("g-popup")).not.toBeNull();
  });

  /*
   * LAST SPEC IN THE FILE, DELIBERATELY. It is the only one that moves the real
   * Playwright mouse, and the pointer stays where it was left for every
   * subsequent spec in the same file — a trigger rendered under it opens on
   * hover, which would silently turn an earlier focus spec's open into a
   * hover-driven one. Keep any new hover coverage below this line.
   */
  it("opens on hover well inside Base UI's default delay when the Provider sets one", async () => {
    // The behavioural proof that `Tooltip.Provider`'s `delay` reaches Base UI.
    // Measured on this machine: `delay={0}` opens in ~70 ms, Base UI's 600 ms
    // default in ~660 ms, so a 400 ms budget separates the two with room on both
    // sides. A provider whose delay was dropped cannot pass this.
    await render(
      <Tooltip.Provider delay={0} closeDelay={0}>
        <Tooltip.Root>
          <Tooltip.Trigger data-testid="h-trigger">Save the draft now</Tooltip.Trigger>
          <Tooltip.Portal>
            <Tooltip.Positioner>
              <Tooltip.Popup data-testid="h-popup">Saves your work</Tooltip.Popup>
            </Tooltip.Positioner>
          </Tooltip.Portal>
        </Tooltip.Root>
      </Tooltip.Provider>,
    );

    await userEvent.hover(part("h-trigger"));

    await expect.poll(() => maybePart("h-popup"), { timeout: 400 }).not.toBeNull();
    expect(part("h-trigger").hasAttribute("data-popup-open")).toBe(true);

    await userEvent.unhover(part("h-trigger"));
    await expect.poll(() => maybePart("h-popup")).toBeNull();
  });
});
