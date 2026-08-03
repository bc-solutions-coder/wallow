import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { PreviewCard } from "./preview-card";

/*
 * PreviewCard behavioural spec (Wallow-m5aq.3.5), shaped after the
 * Wallow-m5aq.3.1 Dialog exemplar:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `previewCardPopupRecipe` and inspecting its return value: a recipe unit
 *      test would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into preview-card.styles.ts.
 *   4. Stories carry the visual coverage (see preview-card.stories.tsx); this
 *      file is only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <a id="base-ui-…" href>                                <- PreviewCard.Trigger
 *     …gains data-popup-open while open. It is an ANCHOR, not a button, and
 *     WITHOUT an href it is not focusable at all (measured: `.focus()` leaves
 *     activeElement on <body>), so every fixture here gives it one.
 *
 *   …and, only while open, portalled onto <body>:
 *   <div data-base-ui-portal>                              <- PreviewCard.Portal
 *     <div role="presentation" data-open
 *          style="pointer-events:none;user-select:none">   <- PreviewCard.Backdrop
 *     <div role="presentation" data-open data-side data-align
 *          style="position:absolute;left;top;--positioner-*;--available-*;
 *                 --anchor-*;--transform-origin">          <- PreviewCard.Positioner
 *       <div data-open data-side data-align tabindex="-1" data-base-ui-focusable
 *            style="--popup-width;--popup-height">         <- PreviewCard.Popup
 *         <div aria-hidden data-open data-side data-align
 *              style="position:absolute;left">             <- PreviewCard.Arrow
 *         <div>                                            <- PreviewCard.Viewport
 *           <div data-current="true">                        (Base UI's own)
 *
 * Eight consequences worth knowing before editing this file. The first four are
 * inherited from the Dialog exemplar and hold unchanged; the last four are
 * specific to a preview card and are the reason this file is not a copy of
 * popover.test.tsx.
 *
 *   - the whole open half is PORTALLED to <body>, so every open-state query goes
 *     through `document.body`, never through `render`'s `container`;
 *   - nothing under PreviewCard.Portal exists in the DOM at all while the card is
 *     closed — these are not hidden elements, they are absent ones;
 *   - CLOSING IS ANIMATION-FRAME-DEFERRED. Base UI gates the unmount behind
 *     `useOpenChangeComplete` -> `useAnimationsFinished`, so the popup is still in
 *     the DOM for at least one rAF after the close resolves (measured: still
 *     present synchronously after a blur that closes it). Every absence assertion
 *     uses `await expect.poll(...)`, never a bare synchronous
 *     `expect(...).toBeNull()`;
 *   - `data-starting-style` / `data-ending-style` only exist DURING a transition,
 *     so no spec here asserts them on an element. They are pinned as
 *     `data-[starting-style]:` / `data-[ending-style]:` modifiers inside the
 *     recipe class sets instead, which is what the catalog actually owns;
 *
 *   - *** OPENING IS DELAYED, EVEN THROUGH FOCUS. *** This is the trap. A tooltip
 *     opens IMMEDIATELY on focus and only hovering waits out a delay, so
 *     tooltip.test.tsx can open through `trigger.focus()` with no timing concern
 *     at all. A preview card applies the trigger's `delay` to BOTH paths
 *     (measured: with Base UI's 600 ms default the popup is absent 400 ms after
 *     `.focus()` and present at 700 ms). Every interactive spec below therefore
 *     renders `delay={0} closeDelay={0}`, and the one spec that does not is the
 *     spec that pins the delay itself. Structural and recipe specs use
 *     `defaultOpen`, which is not delayed at all.
 *   - *** delay/closeDelay LIVE ON THE TRIGGER, *** not on the `Root` and not on
 *     a provider — there is no `PreviewCard.Provider`. A reader arriving from
 *     `Tooltip.Provider` will reach for the wrong part.
 *   - THE POPUP IS NOT IN THE TAB ORDER, AND TABBING DISMISSES THE CARD. Measured:
 *     one Tab from an open card's trigger skips the card's own links entirely,
 *     lands on the next control in document order and unmounts the popup on the
 *     way. There is no focus trap to assert (Dialog) and no
 *     tab-through-then-dismiss trail (Popover) — the card is pointer chrome, and
 *     the spec below pins that so a Base UI release that starts trapping focus
 *     fails loudly.
 *   - NO POINTER BLOCKER, BUT IT DOES DISMISS ON OUTSIDE PRESS. There is no
 *     `modal` prop on this component at all, so Base UI never renders the
 *     `aria-hidden` blocker a modal dialog gets (measured: the portal's only
 *     children are the backdrop and the positioner), and a real
 *     `userEvent.click` inside the popup lands in ~45 ms rather than timing out.
 *     The backdrop is no help for the outside-press path either — Base UI gives
 *     it `pointer-events: none` INLINE, so unlike `Popover.Backdrop` it cannot
 *     catch a press even with Tailwind loaded. The press below is DISPATCHED at
 *     `document.body` instead, which also costs no real mouse movement.
 */

/**
 * Utilities `PreviewCard.Trigger` must render.
 *
 * A deliberate divergence from every other trigger in this catalog: this one is
 * an `<a>` inside running prose, not a button, so it stays INLINE (no
 * `inline-flex items-center justify-center`) and styles its underline instead.
 * Colourless for the usual reason — the surrounding prose owns the link colour,
 * and a `text-*` here would be merged away by tailwind-merge and silently beat
 * it. `data-popup-open` is the ONLY member of `PreviewCardTriggerDataAttributes`
 * (there is no disabled state on this part), so it is the only modifier.
 */
const TRIGGER_CLASSES = [
  "rounded-sm",
  "underline",
  "decoration-dotted",
  "underline-offset-4",
  "transition-colors",
  "data-[popup-open]:decoration-solid",
];

/**
 * Utilities `PreviewCard.Backdrop` must render. `/10` rather than the popover's
 * `/20` and the dialog's `/50`, because a preview card is the lightest chrome in
 * the catalog; `z-40` so it always sits under the `z-50` positioner it dims
 * behind.
 */
const BACKDROP_CLASSES = [
  "fixed",
  "inset-0",
  "z-40",
  "bg-foreground/10",
  "transition-opacity",
  "duration-150",
  "data-[starting-style]:opacity-0",
  "data-[ending-style]:opacity-0",
];

/**
 * Utilities `PreviewCard.Positioner` must render. Base UI owns this element's
 * `position`, `left`, `top` and its custom properties as INLINE styles, so the
 * recipe may only add stacking and focus concerns — never layout that would
 * fight the positioning engine. This is `Select.Positioner`'s rule, not
 * `Dialog.Popup`'s: do NOT copy the dialog's centring utilities onto an anchored
 * overlay.
 */
const POSITIONER_CLASSES = ["z-50", "outline-none"];

/**
 * Utilities `PreviewCard.Popup` must render — the card itself. `w-72` is a fixed
 * width rather than the popover's `min-w-56 max-w-sm`: a preview card previews a
 * known thing, so its width is part of the format and a card that resizes with
 * its content flickers as that content loads. It carries NO positioning; the
 * positioner above already placed it.
 */
const POPUP_CLASSES = [
  "w-72",
  "rounded-lg",
  "border",
  "border-border",
  "bg-popover",
  "p-4",
  "text-popover-foreground",
  "shadow-lg",
  "outline-none",
  "transition-all",
  "duration-150",
  "data-[starting-style]:scale-95",
  "data-[starting-style]:opacity-0",
  "data-[ending-style]:scale-95",
  "data-[ending-style]:opacity-0",
];

/**
 * Utilities `PreviewCard.Arrow` must render. Base UI sets the arrow's `position`
 * and offset inline exactly as it does for the positioner, so this recipe is
 * size and paint only: a square rotated into a diamond wearing the POPUP's own
 * surface and border tokens rather than any hardcoded colour.
 */
const ARROW_CLASSES = [
  "h-2.5",
  "w-2.5",
  "rotate-45",
  "rounded-sm",
  "border",
  "border-border",
  "bg-popover",
];

/**
 * Utilities `PreviewCard.Viewport` must render. The viewport cross-fades the
 * card's contents when one card is shared by several triggers, so it needs a
 * positioning context for the outgoing copy and clipping while the two overlap —
 * and nothing else, since it must not impose a box on a card that has no
 * viewport at all.
 */
const VIEWPORT_CLASSES = ["relative", "overflow-hidden"];

/**
 * Every member `@base-ui/react/preview-card` publishes on its namespace, sorted.
 * Ten, not the popover's thirteen: a preview card has no `Title`, `Description`
 * or `Close`. `Handle` and `createHandle` are the imperative API for detached
 * triggers; they are re-exported unwrapped rather than dropped, so this
 * catalog's namespace keys still mirror Base UI's 1:1.
 */
const BASE_UI_PART_NAMES = [
  "Arrow",
  "Backdrop",
  "Handle",
  "Popup",
  "Portal",
  "Positioner",
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
 * the open half of a preview card is portalled out of the render container.
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
 * the structural and recipe specs want the card already open with no interaction
 * at all, while the open/close specs want to drive it themselves. `delay` and
 * `closeDelay` default to 0 here so a focus-driven open does not wait out Base
 * UI's 600 ms; the spec that pins the delay passes its own.
 */
function FullPreviewCard({
  defaultOpen = false,
  delay = 0,
  closeDelay = 0,
}: {
  readonly defaultOpen?: boolean;
  readonly delay?: number;
  readonly closeDelay?: number;
}): ReactElement {
  return (
    <PreviewCard.Root defaultOpen={defaultOpen}>
      <PreviewCard.Trigger
        data-testid="p-trigger"
        href="https://example.com/ada"
        delay={delay}
        closeDelay={closeDelay}
      >
        Ada Lovelace
      </PreviewCard.Trigger>
      <PreviewCard.Portal data-testid="p-portal">
        <PreviewCard.Backdrop data-testid="p-backdrop" />
        <PreviewCard.Positioner data-testid="p-positioner" sideOffset={8}>
          <PreviewCard.Popup data-testid="p-popup">
            <PreviewCard.Arrow data-testid="p-arrow" />
            <PreviewCard.Viewport data-testid="p-viewport">
              <a href="https://example.com/ada/follow" data-testid="p-follow">
                Follow
              </a>
            </PreviewCard.Viewport>
          </PreviewCard.Popup>
        </PreviewCard.Positioner>
      </PreviewCard.Portal>
    </PreviewCard.Root>
  );
}

/**
 * Renders the full fixture and opens it by FOCUS, with the pointer named as off
 * the trigger.
 *
 * A preview card opens on hover too, and the Playwright pointer position
 * survives between spec FILES — so whichever file ran before this one can leave
 * it sitting exactly where the trigger renders. Measured under CPU contention,
 * the trigger then opens twice (`onOpenChange` fires for the hover-open and
 * again for the focus-open) and blurring no longer closes the card. Unhovering
 * first is what makes the open attributable to the focus. It is NOT instant:
 * the fixture's `delay={0}` is what makes this resolve promptly, so the poll is
 * the wait, not a formality.
 */
async function openPreviewCard(): Promise<void> {
  await render(<FullPreviewCard />);

  await userEvent.unhover(part("p-trigger"));
  part("p-trigger").focus();
  await expect.poll(() => maybePart("p-popup")).not.toBeNull();
}

describe("PreviewCard", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A key added here
    // that Base UI does not have (or a missing one) fails.
    expect(Object.keys(PreviewCard).toSorted()).toEqual(BASE_UI_PART_NAMES);
  });

  it("renders the trigger as an anchor Base UI has claimed, with no aria wiring", async () => {
    // PINS a measured Base UI 1.6.0 behaviour rather than an aspiration: a
    // preview card gets no `aria-haspopup`, no `aria-expanded`, no
    // `aria-controls` and no `aria-describedby` — grepping the whole subpath
    // finds exactly one `aria-*`, the arrow's `aria-hidden`. The catalog
    // therefore documents the card as pointer chrome rather than pretending the
    // popup is announced. If a later Base UI adds the wiring, this fails and the
    // decision gets revisited deliberately.
    await render(<FullPreviewCard />);

    const trigger = part("p-trigger");
    // `data-popup-open` below is a CLOSED-state claim, and a card opens on hover.
    await userEvent.unhover(trigger);

    expect(trigger.tagName).toBe("A");
    expect(trigger.getAttribute("href")).toBe("https://example.com/ada");
    expect(trigger.id).not.toBe("");
    expect(trigger.hasAttribute("data-popup-open")).toBe(false);
    expect(trigger.getAttribute("aria-haspopup")).toBeNull();
    expect(trigger.getAttribute("aria-expanded")).toBeNull();
    expect(trigger.getAttribute("aria-controls")).toBeNull();
    expect(trigger.getAttribute("aria-describedby")).toBeNull();
  });

  it("keeps every portalled part out of the DOM while closed", async () => {
    // Base UI's default: these are absent elements, not hidden ones, so no
    // recipe can be asserted on them until the card opens.
    await render(<FullPreviewCard />);
    await userEvent.unhover(part("p-trigger"));

    expect(maybePart("p-portal")).toBeNull();
    expect(maybePart("p-backdrop")).toBeNull();
    expect(maybePart("p-positioner")).toBeNull();
    expect(maybePart("p-popup")).toBeNull();
    expect(maybePart("p-arrow")).toBeNull();
    expect(maybePart("p-viewport")).toBeNull();
  });

  it("anchors the popup through the positioner, which owns the inline layout", async () => {
    // The structural fact every recipe below depends on, asserted with no
    // interaction at all. Base UI writes `position`/`left`/`top` onto the
    // POSITIONER; the popup gets no positioning of its own, which is why the
    // popup recipe carries none either.
    await render(<FullPreviewCard defaultOpen />);

    const positioner = part("p-positioner");
    expect(positioner.getAttribute("role")).toBe("presentation");
    expect(positioner.style.position).toBe("absolute");
    expect(positioner.hasAttribute("data-open")).toBe(true);
    expect(positioner.hasAttribute("data-side")).toBe(true);
    expect(positioner.hasAttribute("data-align")).toBe(true);

    const popup = part("p-popup");
    expect(positioner.contains(popup)).toBe(true);
    expect(popup.style.position).toBe("");
    expect(popup.hasAttribute("data-open")).toBe(true);
    // No role at all — the popup is not announced as a dialog the way
    // Popover.Popup is, and it is only PROGRAMMATICALLY focusable.
    expect(popup.getAttribute("role")).toBeNull();
    expect(popup.getAttribute("tabindex")).toBe("-1");

    // The arrow is decorative and positioned inline by Base UI too.
    expect(part("p-arrow").getAttribute("aria-hidden")).toBe("true");
    expect(part("p-arrow").style.position).toBe("absolute");
  });

  it("leaves placement to Base UI's inline styles on the positioner and arrow", async () => {
    // The anchored-overlay pin the Popover task (Wallow-m5aq.3.3) added and every
    // sibling inherits: a recipe that reached for `fixed`, `top-1/2` or a
    // translate would fight values Base UI rewrites on every scroll and resize.
    await render(<FullPreviewCard defaultOpen />);

    const positioner = part("p-positioner");
    expect(positioner.style.left).not.toBe("");
    expect(positioner.style.top).not.toBe("");
    expect(positioner.style.getPropertyValue("--positioner-width")).not.toBe("");
    expect(positioner.style.getPropertyValue("--anchor-width")).not.toBe("");

    // The popup's only inline styles are the two size custom properties.
    const popup = part("p-popup");
    expect(popup.style.getPropertyValue("--popup-width")).not.toBe("");
    expect(popup.style.left).toBe("");
    expect(popup.style.transform).toBe("");

    // The arrow is placed on whichever axis the resolved side needs.
    expect(part("p-arrow").style.position).toBe("absolute");
  });

  it("gives the backdrop pointer-events: none inline, so it can never catch a press", async () => {
    // The measured difference from `Popover.Backdrop`, and the reason the
    // outside-press spec below dispatches at document.body rather than pressing
    // the backdrop: this scrim is a dimmer only, in every project and with
    // Tailwind loaded or not.
    await render(<FullPreviewCard defaultOpen />);

    const backdrop = part("p-backdrop");
    expect(backdrop.style.pointerEvents).toBe("none");
    expect(backdrop.getAttribute("role")).toBe("presentation");
    expect(backdrop.hasAttribute("data-open")).toBe(true);
  });

  it("wraps the viewport's content in Base UI's own current-content container", async () => {
    // The viewport is a cross-fade container, not a plain box: Base UI inserts a
    // `data-current` child that it absolutely positions during a transition. The
    // viewport recipe's `relative overflow-hidden` exists for that child, so the
    // child is worth pinning.
    await render(<FullPreviewCard defaultOpen />);

    const viewport = part("p-viewport");
    const current = viewport.firstElementChild;
    expect(current?.getAttribute("data-current")).toBe("true");
    expect(current?.contains(part("p-follow"))).toBe(true);
  });

  it("opens on trigger focus and marks the transition instant", async () => {
    await openPreviewCard();

    expect(part("p-trigger").hasAttribute("data-popup-open")).toBe(true);
    expect(part("p-popup").hasAttribute("data-open")).toBe(true);
    // Base UI skips the enter animation for a focus-driven open. Note this says
    // nothing about the DELAY — the card still waits the trigger's `delay` out
    // before this state exists at all; see the next spec.
    expect(part("p-popup").getAttribute("data-instant")).toBe("focus");
  });

  it("waits out the trigger's delay before opening, on the focus path too", async () => {
    // The spec that pins `delay` as a real, load-bearing prop of the TRIGGER —
    // the one API shape a reader arriving from `Tooltip.Provider` gets wrong.
    // Measured on this machine: Base UI's 600 ms default leaves the popup absent
    // 400 ms after `.focus()` and present at 700 ms, so a 400 ms delay checked at
    // 150 ms and again within 1200 ms clears both edges with room to spare.
    await render(<FullPreviewCard delay={400} closeDelay={0} />);

    // The delay under test is the FOCUS path's, so the pointer must be off.
    await userEvent.unhover(part("p-trigger"));
    part("p-trigger").focus();
    await new Promise((resolve) => {
      setTimeout(resolve, 150);
    });

    expect(maybePart("p-popup")).toBeNull();

    await expect.poll(() => maybePart("p-popup"), { timeout: 1200 }).not.toBeNull();
  });

  it("closes and unmounts the popup when focus leaves the trigger", async () => {
    await render(
      <>
        <FullPreviewCard />
        <button type="button" data-testid="p-elsewhere">
          Elsewhere
        </button>
      </>,
    );

    // A pointer left on the trigger holds the card open after the blur.
    await userEvent.unhover(part("p-trigger"));
    part("p-trigger").focus();
    await expect.poll(() => maybePart("p-popup")).not.toBeNull();

    part("p-elsewhere").focus();

    // Polled, not read once: measured, the popup is STILL in the DOM
    // synchronously after the blur and goes on a later frame.
    await expect.poll(() => maybePart("p-popup")).toBeNull();
    expect(part("p-trigger").hasAttribute("data-popup-open")).toBe(false);
  });

  it("closes and unmounts the popup on Escape", async () => {
    await openPreviewCard();

    await userEvent.keyboard("{Escape}");

    await expect.poll(() => maybePart("p-popup")).toBeNull();
    expect(maybePart("p-positioner")).toBeNull();
    expect(maybePart("p-backdrop")).toBeNull();
  });

  it("keeps the popup out of the tab order and dismisses the card on Tab", async () => {
    // Measured, and the most preview-card-specific behaviour in this file: one
    // Tab from an open card's trigger SKIPS the card's own `Follow` link
    // entirely, lands on the next control in document order, and unmounts the
    // popup on the way. There is no focus trap to assert (Dialog) and no
    // tab-through-then-dismiss trail (Popover) — a preview card is pointer
    // chrome, so anything it offers must be reachable somewhere else too. A Base
    // UI release that starts trapping focus fails here.
    await render(
      <>
        <FullPreviewCard />
        <button type="button" data-testid="p-after">
          After
        </button>
      </>,
    );

    // A pointer left on the trigger holds the card open through the Tab.
    await userEvent.unhover(part("p-trigger"));
    part("p-trigger").focus();
    await expect.poll(() => maybePart("p-popup")).not.toBeNull();

    await userEvent.keyboard("{Tab}");

    expect(document.activeElement).toBe(part("p-after"));
    await expect.poll(() => maybePart("p-popup")).toBeNull();
  });

  it("closes when a press lands outside the card", async () => {
    // The press is DISPATCHED rather than driven with `userEvent.click`, for two
    // reasons: it costs no real mouse movement (the Playwright pointer position
    // survives between specs in a file, and parking it over a trigger reopens
    // that card), and the backdrop that would otherwise be the natural target
    // carries `pointer-events: none` inline.
    const onOpenChange = vi.fn();
    await render(
      <PreviewCard.Root defaultOpen onOpenChange={onOpenChange}>
        <PreviewCard.Trigger data-testid="o-trigger" href="https://example.com/ada">
          Ada Lovelace
        </PreviewCard.Trigger>
        <PreviewCard.Portal>
          <PreviewCard.Positioner>
            <PreviewCard.Popup data-testid="o-popup">Mathematician</PreviewCard.Popup>
          </PreviewCard.Positioner>
        </PreviewCard.Portal>
      </PreviewCard.Root>,
    );
    expect(maybePart("o-popup")).not.toBeNull();
    // A pointer left on the trigger reopens the card the press just dismissed.
    await userEvent.unhover(part("o-trigger"));

    document.body.dispatchEvent(new PointerEvent("pointerdown", { bubbles: true, composed: true }));
    document.body.dispatchEvent(new MouseEvent("click", { bubbles: true, composed: true }));

    await expect.poll(() => maybePart("o-popup")).toBeNull();
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(false);
    expect((onOpenChange.mock.calls[0]?.[1] as { reason?: string })?.reason).toBe("outside-press");
  });

  it("reports open state to onOpenChange with Base UI's event details", async () => {
    // The caller's handler has to survive Base UI's own mergeProps, and the
    // reason it reports is how a fork tells a hover-open from a focus-open.
    const onOpenChange = vi.fn();
    await render(
      <PreviewCard.Root onOpenChange={onOpenChange}>
        <PreviewCard.Trigger
          data-testid="c-trigger"
          href="https://example.com/ada"
          delay={0}
          closeDelay={0}
        >
          Ada Lovelace
        </PreviewCard.Trigger>
        <PreviewCard.Portal>
          <PreviewCard.Positioner>
            <PreviewCard.Popup data-testid="c-popup">Mathematician</PreviewCard.Popup>
          </PreviewCard.Positioner>
        </PreviewCard.Portal>
      </PreviewCard.Root>,
    );

    // `trigger-focus` is the reason under test; a hover-open reports its own.
    await userEvent.unhover(part("c-trigger"));
    part("c-trigger").focus();
    await expect.poll(() => maybePart("c-popup")).not.toBeNull();

    expect(onOpenChange).toHaveBeenCalledTimes(1);
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);
    expect((onOpenChange.mock.calls[0]?.[1] as { reason?: string })?.reason).toBe("trigger-focus");
  });

  it("honours a controlled open prop", async () => {
    const controlled = (open: boolean): ReactElement => (
      <PreviewCard.Root open={open}>
        <PreviewCard.Trigger data-testid="k-trigger" href="https://example.com/ada">
          Ada Lovelace
        </PreviewCard.Trigger>
        <PreviewCard.Portal>
          <PreviewCard.Positioner>
            <PreviewCard.Popup data-testid="k-popup">Mathematician</PreviewCard.Popup>
          </PreviewCard.Positioner>
        </PreviewCard.Portal>
      </PreviewCard.Root>
    );

    const { rerender } = await render(controlled(false));
    expect(maybePart("k-popup")).toBeNull();

    // A controlled open is not delayed: `delay` gates the trigger's own
    // interactions, not the caller's state.
    await rerender(controlled(true));
    expect(part("k-popup").hasAttribute("data-open")).toBe(true);
    expect(part("k-trigger").hasAttribute("data-popup-open")).toBe(true);

    await rerender(controlled(false));
    await expect.poll(() => maybePart("k-popup")).toBeNull();
  });

  it("renders the trigger with its recipe", async () => {
    await render(<FullPreviewCard />);

    expect(classSet(part("p-trigger"))).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("renders the backdrop and positioner with their recipes", async () => {
    await render(<FullPreviewCard defaultOpen />);

    expect(classSet(part("p-backdrop"))).toEqual(BACKDROP_CLASSES.toSorted());
    expect(classSet(part("p-positioner"))).toEqual(POSITIONER_CLASSES.toSorted());
  });

  it("renders the popup, arrow and viewport with their recipes", async () => {
    await render(<FullPreviewCard defaultOpen />);

    expect(classSet(part("p-popup"))).toEqual(POPUP_CLASSES.toSorted());
    expect(classSet(part("p-arrow"))).toEqual(ARROW_CLASSES.toSorted());
    expect(classSet(part("p-viewport"))).toEqual(VIEWPORT_CLASSES.toSorted());
  });

  it("lets a caller className override popup and positioner recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both backgrounds and both widths on and
    // fails.
    await render(
      <PreviewCard.Root defaultOpen>
        <PreviewCard.Trigger data-testid="v-trigger" href="https://example.com/ada">
          Ada Lovelace
        </PreviewCard.Trigger>
        <PreviewCard.Portal>
          <PreviewCard.Positioner data-testid="v-positioner" className="z-10">
            <PreviewCard.Popup data-testid="v-popup" className="w-80 bg-accent">
              Mathematician
            </PreviewCard.Popup>
          </PreviewCard.Positioner>
        </PreviewCard.Portal>
      </PreviewCard.Root>,
    );

    const positioner = part("v-positioner");
    expect(positioner.classList.contains("z-10")).toBe(true);
    expect(positioner.classList.contains("z-50")).toBe(false);
    expect(positioner.classList.contains("outline-none")).toBe(true);

    const popup = part("v-popup");
    expect(popup.classList.contains("w-80")).toBe(true);
    expect(popup.classList.contains("w-72")).toBe(false);
    expect(popup.classList.contains("bg-accent")).toBe(true);
    expect(popup.classList.contains("bg-popover")).toBe(false);
    expect(popup.classList.contains("border-border")).toBe(true);
    expect(popup.classList.contains("data-[ending-style]:scale-95")).toBe(true);
  });

  it("carries the popup recipe onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes.
    await render(
      <PreviewCard.Root defaultOpen>
        <PreviewCard.Trigger data-testid="r-trigger" href="https://example.com/ada">
          Ada Lovelace
        </PreviewCard.Trigger>
        <PreviewCard.Portal>
          <PreviewCard.Positioner>
            <PreviewCard.Popup data-testid="r-popup" render={<article />}>
              Mathematician
            </PreviewCard.Popup>
          </PreviewCard.Positioner>
        </PreviewCard.Portal>
      </PreviewCard.Root>,
    );

    const popup = part("r-popup");
    expect(popup.tagName).toBe("ARTICLE");
    expect(classSet(popup)).toEqual(POPUP_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <PreviewCard.Root defaultOpen>
        <PreviewCard.Trigger
          data-testid="ada-link"
          href="https://example.com/ada"
          rel="author"
          delay={0}
        >
          Ada Lovelace
        </PreviewCard.Trigger>
        <PreviewCard.Portal>
          <PreviewCard.Positioner>
            <PreviewCard.Popup data-testid="ada-card" lang="en">
              Mathematician
            </PreviewCard.Popup>
          </PreviewCard.Positioner>
        </PreviewCard.Portal>
      </PreviewCard.Root>,
    );

    expect(part("ada-link").getAttribute("rel")).toBe("author");
    expect(part("ada-card").getAttribute("lang")).toBe("en");
  });

  /*
   * THE LAST TWO SPECS MOVE THE REAL PLAYWRIGHT MOUSE, and the pointer stays
   * where it was left for every subsequent spec in the same file — a trigger
   * rendered under it opens on hover, which would silently turn an earlier
   * focus-driven open into a hover-driven one. Keep any new pointer coverage
   * below this line.
   */

  it("renders no pointer blocker, so the card's own controls stay clickable", async () => {
    // A preview card has no `modal` prop at all, so it never gets the fixed
    // `aria-hidden` blocker a modal dialog renders over the whole window
    // (measured: the portal's only children are the backdrop and the positioner).
    // That is why a real `userEvent.click` inside the popup lands here in ~45 ms
    // instead of timing out the way dialog.test.tsx's does.
    const onFollow = vi.fn();
    await render(
      <PreviewCard.Root defaultOpen>
        <PreviewCard.Trigger data-testid="b-trigger" href="https://example.com/ada">
          Ada Lovelace
        </PreviewCard.Trigger>
        <PreviewCard.Portal data-testid="b-portal">
          <PreviewCard.Backdrop data-testid="b-backdrop" />
          <PreviewCard.Positioner>
            <PreviewCard.Popup data-testid="b-popup">
              <button type="button" data-testid="b-follow" onClick={onFollow}>
                Follow
              </button>
            </PreviewCard.Popup>
          </PreviewCard.Positioner>
        </PreviewCard.Portal>
      </PreviewCard.Root>,
    );

    expect(part("b-portal").children.length).toBe(2);
    expect(
      document.body.querySelector("[data-base-ui-portal] > [role=presentation][aria-hidden=true]"),
    ).toBeNull();

    await userEvent.click(part("b-follow"));

    expect(onFollow).toHaveBeenCalledTimes(1);
  });

  it("opens on hover well inside Base UI's default delay when the trigger sets one", async () => {
    // The behavioural proof that the TRIGGER's `delay` reaches Base UI on the
    // pointer path, which is the path a preview card exists for. Measured on this
    // machine: `delay={0}` opens in ~40 ms and Base UI's 600 ms default in
    // ~660 ms, so a 400 ms budget separates the two with room on both sides. A
    // wrapper that dropped the prop cannot pass this.
    await render(
      <PreviewCard.Root>
        <PreviewCard.Trigger
          data-testid="h-trigger"
          href="https://example.com/ada"
          delay={0}
          closeDelay={0}
        >
          Ada Lovelace, mathematician
        </PreviewCard.Trigger>
        <PreviewCard.Portal>
          <PreviewCard.Positioner>
            <PreviewCard.Popup data-testid="h-popup">Mathematician</PreviewCard.Popup>
          </PreviewCard.Positioner>
        </PreviewCard.Portal>
      </PreviewCard.Root>,
    );

    await userEvent.hover(part("h-trigger"));

    await expect.poll(() => maybePart("h-popup"), { timeout: 400 }).not.toBeNull();
    expect(part("h-trigger").hasAttribute("data-popup-open")).toBe(true);

    await userEvent.unhover(part("h-trigger"));
    await expect.poll(() => maybePart("h-popup")).toBeNull();
  });
});
