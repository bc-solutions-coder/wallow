import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { buttonRecipe } from "../button/button.styles";
import { AlertDialog } from "./alert-dialog";

/*
 * Alert Dialog behavioural spec (Wallow-m5aq.3.2), shaped after the
 * Wallow-m5aq.3.1 Dialog exemplar:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `alertDialogPopupRecipe` and inspecting its return value: a recipe unit
 *      test would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into alert-dialog.styles.ts.
 *   4. Stories carry the visual coverage (see alert-dialog.stories.tsx); this
 *      file is only for the edges a screenshot cannot make.
 *
 * WHAT MAKES AN ALERT DIALOG DIFFERENT FROM A DIALOG, measured against
 * @base-ui/react 1.6.0 rather than read from the docs: `AlertDialog.Root` is the
 * only part with its own runtime, and all it does is call Dialog's
 * `useRenderDialogRoot(props, 'alert-dialog')`, which forces `modal`, forces
 * `disablePointerDismissal`, and swaps the popup's role. So:
 *
 *   - the popup is `role="alertdialog"`, not `role="dialog"`;
 *   - PRESSING THE BACKDROP DOES NOT CLOSE IT (measured — the popup is still
 *     mounted after the press). This is the reason the component exists, and it
 *     is the one behaviour here that would silently regress into a plain Dialog;
 *   - Escape still DOES close it — `escapeKey` is independent of pointer
 *     dismissal;
 *   - the trigger still advertises `aria-haspopup="dialog"`, NOT "alertdialog"
 *     (measured — Base UI hard-codes it in DialogTrigger, which AlertDialogTrigger
 *     literally is). Pinned below so nobody "fixes" it.
 *
 * ANATOMY, measured in this browser:
 *
 *   <button aria-haspopup="dialog" aria-expanded data-base-ui-click-trigger>  <- Trigger
 *     …gains data-popup-open and aria-controls="<popup id>" while open
 *
 *   …and, only while open, portalled onto <body>:
 *   <div data-base-ui-portal>                                    <- AlertDialog.Portal
 *     <div role="presentation" aria-hidden style="position:fixed;inset:0">
 *                                       ^- Base UI's OWN pointer blocker, see below
 *     <div data-open role="presentation" aria-hidden data-base-ui-inert>  <- Backdrop
 *     <div data-open role="presentation">                        <- AlertDialog.Viewport
 *       <span data-base-ui-focus-guard>            (Base UI's own, not a part)
 *       <div data-open role="alertdialog" tabindex="-1" aria-labelledby aria-describedby>
 *                                                                <- AlertDialog.Popup
 *         <h2 id>                                                <- AlertDialog.Title
 *         <p id>                                                 <- AlertDialog.Description
 *         <button>                                               <- AlertDialog.Close (cancel)
 *         <button>                                               <- AlertDialog.Close (confirm)
 *       <span data-base-ui-focus-guard>
 *
 * The six consequences the Dialog exemplar established, all of which hold here:
 *
 *   - the whole open half is PORTALLED to <body>, so every open-state query goes
 *     through `document.body`, never through `render`'s `container`;
 *   - nothing under AlertDialog.Portal exists in the DOM at all while closed —
 *     these are absent elements, not hidden ones;
 *   - BASE UI ALWAYS RENDERS ONE MORE ELEMENT THAN YOU WROTE: an unstyleable
 *     `<div role="presentation" aria-hidden style="position:fixed;inset:0">` sits
 *     first inside the portal to block outside pointer events, whether or not a
 *     Backdrop is rendered. Its position is an INLINE style, so it covers the
 *     window even here where no Tailwind is loaded — while the popup, whose
 *     `z-50` comes from a recipe class, gets no stacking at all. `userEvent` from
 *     `vitest/browser` drives REAL Playwright input, which hit-tests the click
 *     point, so a click on anything INSIDE an open popup hits that blocker and
 *     times out on the actionability check. Pointer interaction inside the popup
 *     therefore uses a direct `element.click()` here. Realistic pointer coverage
 *     lives in alert-dialog.stories.tsx, where `userEvent` is
 *     `@testing-library/user-event` and dispatches synthetic events with no
 *     hit-testing at all;
 *   - CLOSING IS ANIMATION-FRAME-DEFERRED. Base UI gates the unmount behind
 *     `useOpenChangeComplete` -> `useAnimationsFinished`, so every absence
 *     assertion uses `await expect.poll(...)`, never a bare synchronous
 *     `expect(...).toBeNull()`. (Escape happened to be already-gone synchronously
 *     when measured — that is not something to rely on.)
 *   - `data-starting-style` / `data-ending-style` only exist DURING a transition
 *     (a settled open popup's dataset is just `{open, baseUiFocusable, testid}`),
 *     so no spec here asserts them on an element. They are pinned as
 *     `data-[starting-style]:` / `data-[ending-style]:` modifiers inside the
 *     recipe class sets instead, which is what the catalog actually owns;
 *   - focus only moves into the popup when it is opened by the TRIGGER. A
 *     `defaultOpen` alert dialog leaves focus on <body> (measured), so every focus
 *     assertion below goes through `openAlert()` rather than `defaultOpen`.
 */

/**
 * Utilities `AlertDialog.Trigger` must render. Deliberately colourless, on the
 * Dialog exemplar's reasoning: a trigger is routinely composed onto a real
 * `Button` via `render`, and a background here would be merged away by
 * tailwind-merge and silently beat the Button's own. Note the asymmetry with
 * CLOSE_CLASSES below — that part has no other element to compose onto, so it is
 * the one that carries the button styling.
 */
const TRIGGER_CLASSES = [
  "inline-flex",
  "items-center",
  "justify-center",
  "rounded-md",
  "text-sm",
  "font-medium",
  "transition-colors",
  "data-[disabled]:opacity-50",
];

/** Utilities `AlertDialog.Backdrop` must render. */
const BACKDROP_CLASSES = [
  "fixed",
  "inset-0",
  "z-50",
  "bg-foreground/50",
  "transition-opacity",
  "duration-150",
  "data-[starting-style]:opacity-0",
  "data-[ending-style]:opacity-0",
];

/**
 * Utilities `AlertDialog.Viewport` must render. This part is OPTIONAL — the popup
 * positions itself, so an alert dialog works with or without a viewport — which
 * is why the recipe may only add the scroll region and stacking.
 */
const VIEWPORT_CLASSES = ["fixed", "inset-0", "z-50", "overflow-y-auto", "outline-none"];

/**
 * Utilities `AlertDialog.Popup` must render. Base UI positions NOTHING for a
 * dialog of either kind (measured: the popup's only inline style is
 * `--nested-dialogs`), so this recipe owns the centring outright, exactly like
 * the Dialog exemplar's.
 *
 * `max-w-md` is the one deliberate divergence from `dialogPopupRecipe`'s
 * `max-w-lg`: an alert dialog holds one question and two buttons, never a form,
 * so it is narrower than a general-purpose dialog.
 */
const POPUP_CLASSES = [
  "fixed",
  "top-1/2",
  "left-1/2",
  "z-50",
  "w-full",
  "max-w-md",
  "-translate-x-1/2",
  "-translate-y-1/2",
  "rounded-lg",
  "border",
  "border-border",
  "bg-popover",
  "p-6",
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

/** Utilities `AlertDialog.Title` must render. */
const TITLE_CLASSES = ["text-lg", "font-semibold", "text-foreground"];

/** Utilities `AlertDialog.Description` must render. */
const DESCRIPTION_CLASSES = ["mt-2", "text-sm", "text-muted-foreground"];

/**
 * Utilities `AlertDialog.Close` must render, per variant. Base UI ships no
 * Action/Cancel part — every button in an alert dialog's footer is a `Close` —
 * so this is the part that has to look like a button, and the green phase builds
 * it from `buttonRecipe` rather than restating the button's utilities.
 *
 * `w-full` is deliberately ABSENT even though `buttonRecipe` sets it: the recipe
 * appends `w-auto`, and `cn()`/tailwind-merge collapses the pair. Two buttons
 * sit side by side in an alert's footer row, so the app-form full width the
 * Button component defaults to is wrong here. The
 * "builds the close recipe from the button's own recipe" spec below asserts that
 * collapse against `buttonRecipe`'s LIVE output, so this list cannot drift away
 * from the button without failing.
 *
 * The hover, focus-visible and reduced-motion utilities below arrive from the
 * button too, and arrive here BY DESIGN: an alert's footer buttons are real
 * buttons and get the same keyboard and pointer affordances as any other.
 */
const CLOSE_CANCEL_CLASSES = [
  "inline-flex",
  "w-auto",
  "items-center",
  "justify-center",
  "rounded-md",
  "px-3",
  "py-2",
  "text-sm",
  "font-medium",
  "outline-none",
  "motion-safe:transition-colors",
  "focus-visible:ring-2",
  "focus-visible:ring-ring",
  "data-[disabled]:opacity-50",
  "bg-secondary",
  "text-secondary-foreground",
  "hover:bg-secondary/80",
];

/** The same, for the confirm button of a destructive alert. */
const CLOSE_DESTRUCTIVE_CLASSES = [
  "inline-flex",
  "w-auto",
  "items-center",
  "justify-center",
  "rounded-md",
  "px-3",
  "py-2",
  "text-sm",
  "font-medium",
  "outline-none",
  "motion-safe:transition-colors",
  "focus-visible:ring-2",
  "focus-visible:ring-ring",
  "data-[disabled]:opacity-50",
  "bg-destructive",
  "text-destructive-foreground",
  "hover:bg-destructive/90",
];

/**
 * Every member `@base-ui/react/alert-dialog` publishes on its namespace, sorted
 * — the same eleven as `dialog`, because seven of them ARE Dialog's own runtime
 * re-exported (alert-dialog/index.parts.d.ts). `Handle` and `createHandle` are
 * the imperative open/close API for detached triggers; they are re-exported
 * unwrapped rather than dropped, so this catalog's namespace keys still mirror
 * Base UI's 1:1.
 */
const BASE_UI_PART_NAMES = [
  "Backdrop",
  "Close",
  "Description",
  "Handle",
  "Popup",
  "Portal",
  "Root",
  "Title",
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
 * the open half of an alert dialog is portalled out of the render container.
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
 * Every part at once, so one fixture can carry the whole anatomy. The two `Close`
 * parts are the realistic footer — a cancel and a confirm — and they double as
 * the two tabbable elements the focus trap cycles through.
 */
function FullAlertDialog(): ReactElement {
  return (
    <AlertDialog.Root>
      <AlertDialog.Trigger data-testid="a-trigger">Delete project</AlertDialog.Trigger>
      <AlertDialog.Portal data-testid="a-portal">
        <AlertDialog.Backdrop data-testid="a-backdrop" />
        <AlertDialog.Viewport data-testid="a-viewport">
          <AlertDialog.Popup data-testid="a-popup">
            <AlertDialog.Title data-testid="a-title">Delete project</AlertDialog.Title>
            <AlertDialog.Description data-testid="a-description">
              This cannot be undone.
            </AlertDialog.Description>
            <AlertDialog.Close data-testid="a-cancel">Cancel</AlertDialog.Close>
            <AlertDialog.Close data-testid="a-confirm" variant="destructive">
              Delete
            </AlertDialog.Close>
          </AlertDialog.Popup>
        </AlertDialog.Viewport>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}

/**
 * Renders the full fixture and opens it through the trigger.
 *
 * Opening by TRIGGER rather than `defaultOpen` is load-bearing for every focus
 * assertion here: a `defaultOpen` alert dialog leaves `document.activeElement` on
 * `<body>` (measured), because Base UI only runs its focus-management pass for an
 * open transition it actually observed.
 */
async function openAlert(): Promise<void> {
  await render(<FullAlertDialog />);

  await userEvent.click(part("a-trigger"));
  expect(part("a-trigger").getAttribute("aria-expanded")).toBe("true");
}

describe("AlertDialog", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A key added here
    // that Base UI does not have (or a missing one) fails.
    expect(Object.keys(AlertDialog).toSorted()).toEqual(BASE_UI_PART_NAMES);
  });

  it("renders the trigger as a button that advertises a dialog popup", async () => {
    // `aria-haspopup` is "dialog" and not "alertdialog": AlertDialogTrigger IS
    // DialogTrigger, which hard-codes the value. Pinned so it is not "corrected".
    await render(<FullAlertDialog />);

    const trigger = part("a-trigger");
    expect(trigger.tagName).toBe("BUTTON");
    expect(trigger.getAttribute("aria-haspopup")).toBe("dialog");
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
    expect(trigger.hasAttribute("data-popup-open")).toBe(false);
  });

  it("keeps every portalled part out of the DOM while closed", async () => {
    // Base UI's default: these are absent elements, not hidden ones, so no
    // recipe can be asserted on them until the alert opens.
    await render(<FullAlertDialog />);

    expect(maybePart("a-portal")).toBeNull();
    expect(maybePart("a-backdrop")).toBeNull();
    expect(maybePart("a-viewport")).toBeNull();
    expect(maybePart("a-popup")).toBeNull();
    expect(maybePart("a-title")).toBeNull();
  });

  it("opens with role=alertdialog when the trigger is clicked", async () => {
    // The role is the whole reason this is a separate component from Dialog:
    // assistive technology treats an alertdialog as an interruption.
    await openAlert();

    const popup = part("a-popup");
    expect(popup.getAttribute("role")).toBe("alertdialog");
    expect(popup.hasAttribute("data-open")).toBe(true);
    expect(part("a-backdrop").hasAttribute("data-open")).toBe(true);
  });

  it("marks the trigger data-popup-open and points aria-controls at the popup", async () => {
    await openAlert();

    const trigger = part("a-trigger");
    expect(trigger.hasAttribute("data-popup-open")).toBe(true);
    expect(trigger.getAttribute("aria-controls")).toBe(part("a-popup").id);
  });

  it("names the popup with the title and describes it with the description", async () => {
    // Base UI wires these ids itself; the wrappers must not disturb them.
    await openAlert();

    const popup = part("a-popup");
    expect(popup.getAttribute("aria-labelledby")).toBe(part("a-title").id);
    expect(popup.getAttribute("aria-describedby")).toBe(part("a-description").id);
    expect(part("a-title").tagName).toBe("H2");
    expect(part("a-description").tagName).toBe("P");
  });

  it("renders the trigger with its recipe", async () => {
    await render(<FullAlertDialog />);

    expect(classSet(part("a-trigger"))).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("renders the backdrop, viewport and popup with their recipes", async () => {
    await openAlert();

    expect(classSet(part("a-backdrop"))).toEqual(BACKDROP_CLASSES.toSorted());
    expect(classSet(part("a-viewport"))).toEqual(VIEWPORT_CLASSES.toSorted());
    expect(classSet(part("a-popup"))).toEqual(POPUP_CLASSES.toSorted());
  });

  it("renders the title and description with their recipes", async () => {
    await openAlert();

    expect(classSet(part("a-title"))).toEqual(TITLE_CLASSES.toSorted());
    expect(classSet(part("a-description"))).toEqual(DESCRIPTION_CLASSES.toSorted());
  });

  it("renders each close button with its variant of the close recipe", async () => {
    // `secondary` is the default variant, so the cancel button opts into nothing.
    await openAlert();

    expect(classSet(part("a-cancel"))).toEqual(CLOSE_CANCEL_CLASSES.toSorted());
    expect(classSet(part("a-confirm"))).toEqual(CLOSE_DESTRUCTIVE_CLASSES.toSorted());
  });

  it("builds the close recipe from the button's own recipe rather than restating it", async () => {
    // The one deliberate cross-component import in this catalog (bead
    // Wallow-m5aq.3.2, design doc §2). Asserted against `buttonRecipe`'s LIVE
    // output rather than a copied class list, so a change to button.styles.ts
    // moves this spec with it instead of letting the two silently drift.
    await openAlert();

    const rendered = new Set(classSet(part("a-confirm")));
    for (const utility of buttonRecipe({ variant: "destructive" }).split(" ")) {
      // The single exception, and the reason it is an exception: an alert's two
      // buttons share a footer row, so the recipe appends `w-auto` and lets
      // tailwind-merge drop the Button component's app-form `w-full`.
      if (utility !== "w-full") {
        expect(rendered.has(utility), `close is missing the button utility ${utility}`).toBe(true);
      }
    }
    expect(rendered.has("w-full")).toBe(false);
    expect(rendered.has("w-auto")).toBe(true);
  });

  it("moves focus into the popup when the trigger opens it", async () => {
    // Base UI's default `initialFocus`: the first tabbable element inside the
    // popup, not the popup itself — here the cancel button, which is the safe
    // default for a destructive alert.
    await openAlert();

    expect(part("a-popup").contains(document.activeElement)).toBe(true);
    expect(document.activeElement).toBe(part("a-cancel"));
  });

  it("traps Tab inside the popup", async () => {
    await openAlert();

    // Two tabbable elements inside, so four presses must wrap twice. Focus
    // leaving the popup even once — onto the trigger, the body, or one of Base
    // UI's focus guards — fails.
    for (let index = 0; index < 4; index += 1) {
      await userEvent.keyboard("{Tab}");
      expect(part("a-popup").contains(document.activeElement)).toBe(true);
    }
  });

  it("stays open when the backdrop is pressed", async () => {
    // THE behaviour that separates an alert dialog from a dialog:
    // `useRenderDialogRoot` forces `disablePointerDismissal` for the
    // 'alert-dialog' mode and removes the prop from the type, so there is no way
    // to opt back out. A regression to a plain Dialog fails here and nowhere
    // else. Measured: the popup is still mounted after the press.
    await openAlert();

    part("a-backdrop").click();

    // Proving a NON-event needs a beat: closing is animation-frame-deferred, so
    // reading the DOM straight after the press would pass even against a
    // component that had accepted the dismissal. 100ms is several frames.
    await new Promise((resolve) => {
      setTimeout(resolve, 100);
    });

    expect(maybePart("a-popup")).not.toBeNull();
    expect(part("a-popup").hasAttribute("data-open")).toBe(true);
    expect(part("a-trigger").getAttribute("aria-expanded")).toBe("true");

    // …and it was closable the whole time, so the press above really was ignored
    // rather than the alert being stuck open for some unrelated reason.
    await userEvent.keyboard("{Escape}");
    await expect.poll(() => maybePart("a-popup")).toBeNull();
  });

  it("closes and unmounts the popup on Escape", async () => {
    // Escape is NOT pointer dismissal, so it still closes an alert dialog.
    await openAlert();

    await userEvent.keyboard("{Escape}");

    // Polled, not read once: the unmount is gated behind an animation frame.
    await expect.poll(() => maybePart("a-popup")).toBeNull();
    expect(maybePart("a-backdrop")).toBeNull();
    expect(part("a-trigger").getAttribute("aria-expanded")).toBe("false");
  });

  it("closes and unmounts the popup when a close part is pressed", async () => {
    // A direct DOM click rather than `userEvent.click`: Base UI's own fixed
    // pointer blocker covers the unstyled popup in this project, so Playwright's
    // actionability check would never resolve. See the header.
    await openAlert();

    part("a-confirm").click();

    await expect.poll(() => maybePart("a-popup")).toBeNull();
  });

  it("returns focus to the trigger after closing", async () => {
    await openAlert();

    await userEvent.keyboard("{Escape}");
    await expect.poll(() => maybePart("a-popup")).toBeNull();

    expect(document.activeElement).toBe(part("a-trigger"));
  });

  it("reports open state to onOpenChange", async () => {
    // The caller's handler has to survive Base UI's own mergeProps.
    const onOpenChange = vi.fn();
    await render(
      <AlertDialog.Root onOpenChange={onOpenChange}>
        <AlertDialog.Trigger data-testid="o-trigger">Delete</AlertDialog.Trigger>
        <AlertDialog.Portal>
          <AlertDialog.Popup data-testid="o-popup">
            <AlertDialog.Title>Delete project</AlertDialog.Title>
          </AlertDialog.Popup>
        </AlertDialog.Portal>
      </AlertDialog.Root>,
    );

    await userEvent.click(part("o-trigger"));

    expect(onOpenChange).toHaveBeenCalledTimes(1);
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);
  });

  it("honours a controlled open prop", async () => {
    const { rerender } = await render(
      <AlertDialog.Root open={false}>
        <AlertDialog.Portal>
          <AlertDialog.Popup data-testid="c-popup">
            <AlertDialog.Title>Delete project</AlertDialog.Title>
          </AlertDialog.Popup>
        </AlertDialog.Portal>
      </AlertDialog.Root>,
    );

    expect(maybePart("c-popup")).toBeNull();

    await rerender(
      <AlertDialog.Root open>
        <AlertDialog.Portal>
          <AlertDialog.Popup data-testid="c-popup">
            <AlertDialog.Title>Delete project</AlertDialog.Title>
          </AlertDialog.Popup>
        </AlertDialog.Portal>
      </AlertDialog.Root>,
    );

    expect(part("c-popup").hasAttribute("data-open")).toBe(true);
  });

  it("lets a caller className override popup and backdrop recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both background classes on and fails.
    await render(
      <AlertDialog.Root defaultOpen>
        <AlertDialog.Portal>
          <AlertDialog.Backdrop data-testid="v-backdrop" className="bg-accent" />
          <AlertDialog.Popup data-testid="v-popup" className="max-w-sm bg-accent">
            <AlertDialog.Title>Delete project</AlertDialog.Title>
          </AlertDialog.Popup>
        </AlertDialog.Portal>
      </AlertDialog.Root>,
    );

    const backdrop = part("v-backdrop");
    expect(backdrop.classList.contains("bg-accent")).toBe(true);
    expect(backdrop.classList.contains("bg-foreground/50")).toBe(false);
    expect(backdrop.classList.contains("fixed")).toBe(true);

    const popup = part("v-popup");
    expect(popup.classList.contains("bg-accent")).toBe(true);
    expect(popup.classList.contains("bg-popover")).toBe(false);
    expect(popup.classList.contains("max-w-sm")).toBe(true);
    expect(popup.classList.contains("max-w-md")).toBe(false);
    expect(popup.classList.contains("border-border")).toBe(true);
    expect(popup.classList.contains("data-[ending-style]:scale-95")).toBe(true);
  });

  it("lets a caller className override the close recipe's inherited button utilities", async () => {
    // The composition has to stay overridable end to end: a utility that came
    // from `buttonRecipe`, two files away, must still lose to the caller's.
    await render(
      <AlertDialog.Root defaultOpen>
        <AlertDialog.Portal>
          <AlertDialog.Popup>
            <AlertDialog.Title>Delete project</AlertDialog.Title>
            <AlertDialog.Close data-testid="v-close" variant="destructive" className="bg-muted">
              Delete
            </AlertDialog.Close>
          </AlertDialog.Popup>
        </AlertDialog.Portal>
      </AlertDialog.Root>,
    );

    const close = part("v-close");
    expect(close.classList.contains("bg-muted")).toBe(true);
    expect(close.classList.contains("bg-destructive")).toBe(false);
    expect(close.classList.contains("text-destructive-foreground")).toBe(true);
    expect(close.classList.contains("w-auto")).toBe(true);
  });

  it("carries the popup recipe onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes.
    await render(
      <AlertDialog.Root defaultOpen>
        <AlertDialog.Portal>
          <AlertDialog.Popup data-testid="r-popup" render={<section />}>
            <AlertDialog.Title>Delete project</AlertDialog.Title>
          </AlertDialog.Popup>
        </AlertDialog.Portal>
      </AlertDialog.Root>,
    );

    const popup = part("r-popup");
    expect(popup.tagName).toBe("SECTION");
    expect(classSet(popup)).toEqual(POPUP_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <AlertDialog.Root defaultOpen>
        <AlertDialog.Portal>
          <AlertDialog.Popup data-testid="delete-project" aria-label="Delete project">
            <AlertDialog.Title>Delete project</AlertDialog.Title>
          </AlertDialog.Popup>
        </AlertDialog.Portal>
      </AlertDialog.Root>,
    );

    expect(part("delete-project").getAttribute("aria-label")).toBe("Delete project");
  });
});
