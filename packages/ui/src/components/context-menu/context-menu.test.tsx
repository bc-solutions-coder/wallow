import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Menu } from "../menu/menu";
import { ContextMenu } from "./context-menu";

/*
 * Context Menu behavioural spec (Wallow-m5aq.3.7), shaped after the
 * Wallow-m5aq.3.1 Dialog exemplar and the Wallow-m5aq.3.6 Menu spec it reuses:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `contextMenuTriggerRecipe` and inspecting its return value: a recipe unit
 *      test would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. `TRIGGER_CLASSES` below is the
 *      single source of truth for the one recipe this component owns — the green
 *      phase transcribes it into context-menu.styles.ts.
 *   4. Stories carry the visual coverage (see context-menu.stories.tsx); this
 *      file is only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <div>                                          <- ContextMenu.Trigger
 *     …carries NO attribute of its own while closed: no role, no tabindex, no
 *     aria-haspopup, no aria-expanded, no aria-controls, no inline style.
 *     While open it gains exactly data-popup-open and data-pressed.
 *
 *   …and, only while open, portalled onto <body>:
 *   <div data-base-ui-portal>                      <- ContextMenu.Portal
 *     <div data-open role="presentation" aria-hidden data-base-ui-inert
 *          style="user-select:none">               <- ContextMenu.Backdrop
 *     <div role="presentation" data-base-ui-inert
 *          style="position:fixed;inset:0;user-select:none">
 *                                   ^- Base UI's OWN pointer blocker, see below
 *     <div data-open data-side data-align role="presentation"
 *          style="position:fixed;left:0;top:0;transform:translate(x,y);
 *                 --anchor-width:0px;--anchor-height:0px">
 *                                                  <- ContextMenu.Positioner
 *       <div data-open role="menu" tabindex="-1" aria-orientation="vertical"
 *            data-rootownerid>                     <- ContextMenu.Popup
 *         …then exactly the Menu anatomy: Arrow, Group/GroupLabel, Item,
 *         LinkItem, Separator, CheckboxItem(+Indicator), RadioGroup/RadioItem
 *         (+Indicator), SubmenuRoot/SubmenuTrigger.
 *
 * Six consequences worth knowing before editing this file:
 *
 *   - SEVENTEEN OF THE NINETEEN PARTS ARE MENU'S OWN WRAPPERS, not re-wraps.
 *     `ContextMenu.Popup === Menu.Popup` is asserted below, and every recipe
 *     assertion here therefore pins MENU's class list, which menu.test.tsx owns.
 *     The only recipe this component adds is the trigger's;
 *   - the trigger opens on `contextmenu`, so the open path is
 *     `userEvent.click(trigger, { button: "right" })` — real Playwright input,
 *     which is exactly what a user does. There is NO click-to-open and NO
 *     keyboard-to-open: a context-menu trigger is not focusable;
 *   - THE POPUP IS ANCHORED TO THE CURSOR, NOT TO THE TRIGGER BOX. Base UI feeds
 *     the positioner a zero-size virtual anchor at the pointer, which is why the
 *     positioner reports `--anchor-width: 0px` and `position: fixed` where a
 *     plain menu reports the trigger's width and `position: absolute`;
 *   - A MODAL MENU ALWAYS RENDERS ONE MORE ELEMENT THAN YOU WROTE: Base UI puts
 *     an unstyleable `<div role="presentation" style="position:fixed;inset:0">`
 *     inside the portal to block outside pointer events, whether or not you
 *     render a Backdrop. This project loads no Tailwind, so the popup's `z-50`
 *     is inert and the blocker covers it — a `userEvent.click` on anything
 *     INSIDE the open popup hits the blocker and times out on Playwright's
 *     actionability check. Interaction inside the popup therefore goes through
 *     the KEYBOARD here (which a menu wants anyway) or a direct
 *     `element.click()`. Realistic pointer coverage lives in the stories;
 *   - CLOSING IS ANIMATION-FRAME-DEFERRED and ROVING FOCUS IS ASYNCHRONOUS, the
 *     two Wave-2 timing gotchas: every absence assertion uses
 *     `await expect.poll(...)`, never a bare synchronous read, and so does every
 *     focus assertion. Opening focuses the POPUP itself, not the first row;
 *   - FOCUS IS NOT RESTORED ON CLOSE, and this is the sharpest divergence from
 *     Menu. Menu returns focus to its trigger `<button>`; a context-menu trigger
 *     is a non-focusable `<div>`, so there is nothing to return focus to and
 *     Base UI drops it to `<body>` (measured). No spec here asserts a restore.
 */

/**
 * Utilities `ContextMenu.Trigger` must render — the ONLY recipe this component
 * owns. Deliberately layout-neutral and colourless: the trigger wraps content
 * the caller already styled, so it may add nothing that repaints that content.
 * `select-none` stops a right-drag or long press from starting a text selection
 * (Base UI handles only the touch-callout half itself), and the
 * `data-[popup-open]:` ring marks the area the open menu belongs to.
 */
const TRIGGER_CLASSES = [
  "select-none",
  "rounded-md",
  "outline-none",
  "data-[popup-open]:ring-2",
  "data-[popup-open]:ring-ring",
];

/**
 * Every member `@base-ui/react/context-menu` publishes on its namespace, sorted.
 * NOTE what is absent next to `menu`'s twenty-two: there is no `Viewport` and no
 * `Handle`/`createHandle`. A context menu has one trigger area and no detached
 * imperative opener, so nineteen is the whole surface.
 */
const BASE_UI_PART_NAMES = [
  "Arrow",
  "Backdrop",
  "CheckboxItem",
  "CheckboxItemIndicator",
  "Group",
  "GroupLabel",
  "Item",
  "LinkItem",
  "Popup",
  "Portal",
  "Positioner",
  "RadioGroup",
  "RadioItem",
  "RadioItemIndicator",
  "Root",
  "Separator",
  "SubmenuRoot",
  "SubmenuTrigger",
  "Trigger",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/**
 * The part carrying `data-testid`, searched across the whole document because
 * the open half of a context menu is portalled out of the render container.
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

/** The `data-testid` of whatever currently holds focus, for the polled focus assertions. */
function focusedTestId(): string | null {
  return document.activeElement?.getAttribute("data-testid") ?? null;
}

/**
 * Every part at once, so one fixture can carry the whole anatomy. The rows are
 * given real text labels deliberately: this project loads no Tailwind, so a
 * textless row would measure 0x0 and be unreachable to a real pointer.
 */
function FullContextMenu(): ReactElement {
  return (
    <ContextMenu.Root>
      <ContextMenu.Trigger data-testid="cm-trigger">Right click this area</ContextMenu.Trigger>
      <ContextMenu.Portal data-testid="cm-portal">
        <ContextMenu.Backdrop data-testid="cm-backdrop" />
        <ContextMenu.Positioner data-testid="cm-positioner">
          <ContextMenu.Popup data-testid="cm-popup">
            <ContextMenu.Arrow data-testid="cm-arrow" />
            <ContextMenu.Group data-testid="cm-group">
              <ContextMenu.GroupLabel data-testid="cm-group-label">Project</ContextMenu.GroupLabel>
              <ContextMenu.Item data-testid="cm-item-duplicate">Duplicate</ContextMenu.Item>
              <ContextMenu.Item data-testid="cm-item-rename">Rename</ContextMenu.Item>
              <ContextMenu.LinkItem data-testid="cm-link" href="https://example.com/docs">
                Open docs
              </ContextMenu.LinkItem>
            </ContextMenu.Group>
            <ContextMenu.Separator data-testid="cm-separator" />
            <ContextMenu.CheckboxItem data-testid="cm-checkbox">
              <ContextMenu.CheckboxItemIndicator data-testid="cm-checkbox-indicator" />
              Show grid
            </ContextMenu.CheckboxItem>
            <ContextMenu.RadioGroup data-testid="cm-radio-group" defaultValue="list">
              <ContextMenu.RadioItem data-testid="cm-radio-list" value="list">
                <ContextMenu.RadioItemIndicator data-testid="cm-radio-list-indicator" />
                List
              </ContextMenu.RadioItem>
              <ContextMenu.RadioItem data-testid="cm-radio-grid" value="grid">
                <ContextMenu.RadioItemIndicator data-testid="cm-radio-grid-indicator" />
                Grid
              </ContextMenu.RadioItem>
            </ContextMenu.RadioGroup>
            <ContextMenu.SubmenuRoot>
              <ContextMenu.SubmenuTrigger data-testid="cm-sub-trigger">
                Move to
              </ContextMenu.SubmenuTrigger>
              <ContextMenu.Portal>
                <ContextMenu.Positioner data-testid="cm-sub-positioner">
                  <ContextMenu.Popup data-testid="cm-sub-popup">
                    <ContextMenu.Item data-testid="cm-sub-item">Archive</ContextMenu.Item>
                  </ContextMenu.Popup>
                </ContextMenu.Positioner>
              </ContextMenu.Portal>
            </ContextMenu.SubmenuRoot>
          </ContextMenu.Popup>
        </ContextMenu.Positioner>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  );
}

/**
 * Renders the full fixture and opens it the only way a context menu opens: a
 * real right click on the trigger area, then a wait for Base UI's focus pass to
 * settle on the popup. Right-clicking the TRIGGER is safe even though an open
 * popup is covered by Base UI's pointer blocker — the blocker does not exist
 * until the menu opens.
 */
async function openContextMenu(): Promise<void> {
  await render(<FullContextMenu />);

  await userEvent.click(part("cm-trigger"), { button: "right" });
  await expect.poll(focusedTestId).toBe("cm-popup");
}

/** Steps the roving focus one row and waits for it to land, since it moves a tick late. */
async function pressKey(key: string, expectedTestId: string): Promise<void> {
  await userEvent.keyboard(key);
  await expect.poll(focusedTestId).toBe(expectedTestId);
}

describe("ContextMenu", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. Note the absent
    // Viewport and Handle/createHandle — this subpath genuinely does not publish
    // them, and inventing them here would break the mirror.
    expect(Object.keys(ContextMenu).toSorted()).toEqual(BASE_UI_PART_NAMES);
  });

  it("reuses the Menu component's own wrappers for every shared part", () => {
    // The load-bearing decision this component exists to make. Base UI's
    // context-menu subpath re-exports menu's runtime for these seventeen
    // members, so the catalog re-exports Menu's already-styled wrappers rather
    // than minting a second, identical recipe set that could drift. An identity
    // check is the only assertion that can prove no re-wrap crept back in.
    expect(ContextMenu.Backdrop).toBe(Menu.Backdrop);
    expect(ContextMenu.Portal).toBe(Menu.Portal);
    expect(ContextMenu.Positioner).toBe(Menu.Positioner);
    expect(ContextMenu.Popup).toBe(Menu.Popup);
    expect(ContextMenu.Arrow).toBe(Menu.Arrow);
    expect(ContextMenu.Group).toBe(Menu.Group);
    expect(ContextMenu.GroupLabel).toBe(Menu.GroupLabel);
    expect(ContextMenu.Item).toBe(Menu.Item);
    expect(ContextMenu.LinkItem).toBe(Menu.LinkItem);
    expect(ContextMenu.CheckboxItem).toBe(Menu.CheckboxItem);
    expect(ContextMenu.CheckboxItemIndicator).toBe(Menu.CheckboxItemIndicator);
    expect(ContextMenu.RadioGroup).toBe(Menu.RadioGroup);
    expect(ContextMenu.RadioItem).toBe(Menu.RadioItem);
    expect(ContextMenu.RadioItemIndicator).toBe(Menu.RadioItemIndicator);
    expect(ContextMenu.Separator).toBe(Menu.Separator);
    expect(ContextMenu.SubmenuRoot).toBe(Menu.SubmenuRoot);
    expect(ContextMenu.SubmenuTrigger).toBe(Menu.SubmenuTrigger);
    // …and the two that are NOT shared, so the reuse claim stays honest.
    expect(ContextMenu.Root).not.toBe(Menu.Root);
    expect(ContextMenu.Trigger).not.toBe(Menu.Trigger);
  });

  it("renders the trigger as a bare div that advertises nothing", async () => {
    // Base UI's design, and the reason to reach for Menu instead when the menu
    // needs a keyboard-reachable opener: a right-click area is not a button, has
    // no accessible expanded state and is not in the tab order.
    await render(<FullContextMenu />);

    const trigger = part("cm-trigger");
    expect(trigger.tagName).toBe("DIV");
    expect(trigger.hasAttribute("role")).toBe(false);
    expect(trigger.hasAttribute("tabindex")).toBe(false);
    expect(trigger.hasAttribute("aria-haspopup")).toBe(false);
    expect(trigger.hasAttribute("aria-expanded")).toBe(false);
    expect(trigger.hasAttribute("data-popup-open")).toBe(false);
  });

  it("keeps every portalled part out of the DOM while closed", async () => {
    // Base UI's default: these are absent elements, not hidden ones, so no
    // recipe can be asserted on them until the menu opens.
    await render(<FullContextMenu />);

    expect(maybePart("cm-portal")).toBeNull();
    expect(maybePart("cm-backdrop")).toBeNull();
    expect(maybePart("cm-positioner")).toBeNull();
    expect(maybePart("cm-popup")).toBeNull();
    expect(maybePart("cm-item-duplicate")).toBeNull();
  });

  it("opens the menu on a right click", async () => {
    await openContextMenu();

    const popup = part("cm-popup");
    expect(popup.getAttribute("role")).toBe("menu");
    expect(popup.getAttribute("aria-orientation")).toBe("vertical");
    expect(popup.hasAttribute("data-open")).toBe(true);
    expect(part("cm-backdrop").hasAttribute("data-open")).toBe(true);
  });

  it("marks the trigger data-popup-open and data-pressed while the menu is up", async () => {
    // The two state attributes Base UI's pressable-trigger mapping publishes,
    // and the whole of what a context-menu trigger says about itself. There is
    // no aria-controls: the popup is not labelled by this element.
    await openContextMenu();

    const trigger = part("cm-trigger");
    expect(trigger.hasAttribute("data-popup-open")).toBe(true);
    expect(trigger.hasAttribute("data-pressed")).toBe(true);
    expect(trigger.hasAttribute("aria-controls")).toBe(false);
    expect(part("cm-popup").hasAttribute("aria-labelledby")).toBe(false);
  });

  it("anchors the popup to the cursor rather than to the trigger box", async () => {
    // The one behavioural difference from Menu that is not about the trigger
    // element: Base UI feeds this positioner a ZERO-SIZE virtual anchor at the
    // pointer, so the anchor custom properties are 0px and the positioner is
    // fixed to the viewport rather than absolute inside the page.
    await openContextMenu();

    const positioner = part("cm-positioner");
    expect(positioner.style.position).toBe("fixed");
    expect(positioner.style.getPropertyValue("--anchor-width")).toBe("0px");
    expect(positioner.style.getPropertyValue("--anchor-height")).toBe("0px");
    expect(positioner.style.transform).not.toBe("");
  });

  it("gives every kind of row the role its behaviour promises", async () => {
    // Inherited wholesale from Menu, and pinned here anyway: this is what would
    // break if the reuse were ever replaced by a hand-rolled re-wrap.
    await openContextMenu();

    expect(part("cm-item-duplicate").getAttribute("role")).toBe("menuitem");
    expect(part("cm-link").tagName).toBe("A");
    expect(part("cm-link").getAttribute("role")).toBe("menuitem");
    expect(part("cm-checkbox").getAttribute("role")).toBe("menuitemcheckbox");
    expect(part("cm-checkbox").getAttribute("aria-checked")).toBe("false");
    expect(part("cm-radio-list").getAttribute("role")).toBe("menuitemradio");
    expect(part("cm-radio-list").getAttribute("aria-checked")).toBe("true");
    expect(part("cm-separator").getAttribute("role")).toBe("separator");
    expect(part("cm-group").getAttribute("role")).toBe("group");
    expect(part("cm-group").getAttribute("aria-labelledby")).toBe(part("cm-group-label").id);
    expect(part("cm-sub-trigger").getAttribute("aria-haspopup")).toBe("menu");
  });

  it("renders the trigger with its recipe", async () => {
    await render(<FullContextMenu />);

    expect(classSet(part("cm-trigger"))).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("dresses every shared part in the Menu component's own recipes", async () => {
    // Not a duplicate of menu.test.tsx: that file proves the class lists are
    // right, this one proves a context menu gets THOSE lists and not a copy. The
    // popup's `bg-popover` and the row's `px-2` are read straight off Menu's
    // recipes, so if the shared wrappers were ever swapped for re-wraps with
    // their own strings, this fails.
    await openContextMenu();

    expect(part("cm-popup").classList.contains("bg-popover")).toBe(true);
    expect(part("cm-popup").classList.contains("min-w-32")).toBe(true);
    expect(part("cm-popup").classList.contains("rounded-md")).toBe(true);
    expect(part("cm-popup").classList.contains("p-1")).toBe(true);
    expect(part("cm-positioner").classList.contains("z-50")).toBe(true);
    expect(part("cm-backdrop").classList.contains("fixed")).toBe(true);
    expect(part("cm-item-duplicate").classList.contains("px-2")).toBe(true);
    expect(part("cm-item-duplicate").classList.contains("data-[highlighted]:bg-accent")).toBe(true);
    expect(part("cm-checkbox").classList.contains("pl-8")).toBe(true);
    expect(part("cm-separator").classList.contains("bg-border")).toBe(true);
    expect(part("cm-group-label").classList.contains("text-muted-foreground")).toBe(true);
  });

  it("moves the highlight down and up the rows with the arrow keys", async () => {
    // Opening put focus on the popup, so the keyboard works from there with no
    // pointer involved. Focus is polled at every step because Base UI moves it a
    // tick after the key, and `data-highlighted` follows the same roving pointer.
    await openContextMenu();

    await pressKey("{ArrowDown}", "cm-item-duplicate");
    expect(part("cm-item-duplicate").hasAttribute("data-highlighted")).toBe(true);

    await pressKey("{ArrowDown}", "cm-item-rename");
    expect(part("cm-item-rename").hasAttribute("data-highlighted")).toBe(true);
    expect(part("cm-item-duplicate").hasAttribute("data-highlighted")).toBe(false);

    await pressKey("{ArrowDown}", "cm-link");
    await pressKey("{ArrowUp}", "cm-item-rename");
    await pressKey("{ArrowUp}", "cm-item-duplicate");
  });

  it("loops the highlight past the last row and jumps with Home and End", async () => {
    await openContextMenu();

    await pressKey("{ArrowUp}", "cm-sub-trigger");
    await pressKey("{ArrowDown}", "cm-item-duplicate");
    await pressKey("{End}", "cm-sub-trigger");
    await pressKey("{Home}", "cm-item-duplicate");
  });

  it("opens a submenu with ArrowRight and closes it again with ArrowLeft", async () => {
    await openContextMenu();

    await pressKey("{End}", "cm-sub-trigger");
    await userEvent.keyboard("{ArrowRight}");

    await expect.poll(focusedTestId).toBe("cm-sub-item");
    expect(part("cm-sub-trigger").getAttribute("aria-expanded")).toBe("true");
    expect(part("cm-sub-trigger").hasAttribute("data-popup-open")).toBe(true);
    expect(part("cm-sub-popup").hasAttribute("data-nested")).toBe(true);

    await userEvent.keyboard("{ArrowLeft}");

    await expect.poll(() => maybePart("cm-sub-popup")).toBeNull();
    await expect.poll(focusedTestId).toBe("cm-sub-trigger");
    // Only the submenu closed: the parent menu is still on screen.
    expect(part("cm-popup").hasAttribute("data-open")).toBe(true);
  });

  it("toggles a checkbox row without closing the menu", async () => {
    await openContextMenu();

    await pressKey("{End}", "cm-sub-trigger");
    await pressKey("{ArrowUp}", "cm-radio-grid");
    await pressKey("{ArrowUp}", "cm-radio-list");
    await pressKey("{ArrowUp}", "cm-checkbox");

    await userEvent.keyboard(" ");

    await expect.poll(() => part("cm-checkbox").getAttribute("aria-checked")).toBe("true");
    expect(part("cm-checkbox").hasAttribute("data-checked")).toBe(true);
    // The indicator only exists once the row is checked.
    expect(part("cm-checkbox-indicator").hasAttribute("data-checked")).toBe(true);
    // `closeOnClick` defaults to false for a checkbox row, so the menu stays up.
    expect(part("cm-popup").hasAttribute("data-open")).toBe(true);
  });

  it("moves the selection between radio rows and with it the indicator", async () => {
    await openContextMenu();

    expect(part("cm-radio-list").getAttribute("aria-checked")).toBe("true");
    expect(maybePart("cm-radio-grid-indicator")).toBeNull();

    await pressKey("{End}", "cm-sub-trigger");
    await pressKey("{ArrowUp}", "cm-radio-grid");
    await userEvent.keyboard(" ");

    await expect.poll(() => part("cm-radio-grid").getAttribute("aria-checked")).toBe("true");
    expect(part("cm-radio-list").getAttribute("aria-checked")).toBe("false");
    expect(maybePart("cm-radio-list-indicator")).toBeNull();
    expect(part("cm-radio-grid-indicator").hasAttribute("data-checked")).toBe(true);
  });

  it("closes and unmounts the menu on Escape", async () => {
    await openContextMenu();

    await userEvent.keyboard("{Escape}");

    // Polled, not read once: the unmount is gated behind an animation frame.
    await expect.poll(() => maybePart("cm-popup")).toBeNull();
    expect(maybePart("cm-backdrop")).toBeNull();
    expect(part("cm-trigger").hasAttribute("data-popup-open")).toBe(false);
    expect(part("cm-trigger").hasAttribute("data-pressed")).toBe(false);
  });

  it("closes and unmounts the menu when a plain item is pressed", async () => {
    await openContextMenu();

    // A direct DOM click rather than `userEvent.click`: Base UI's own fixed
    // pointer blocker covers the unstyled popup in this project, so Playwright's
    // actionability check would never resolve. See the header.
    part("cm-item-duplicate").click();

    await expect.poll(() => maybePart("cm-popup")).toBeNull();
  });

  it("reports open state to onOpenChange", async () => {
    // The caller's handler has to survive Base UI's own mergeProps.
    const onOpenChange = vi.fn();
    await render(
      <ContextMenu.Root onOpenChange={onOpenChange}>
        <ContextMenu.Trigger data-testid="o-trigger">Right click</ContextMenu.Trigger>
        <ContextMenu.Portal>
          <ContextMenu.Positioner>
            <ContextMenu.Popup data-testid="o-popup">
              <ContextMenu.Item data-testid="o-item">Duplicate</ContextMenu.Item>
            </ContextMenu.Popup>
          </ContextMenu.Positioner>
        </ContextMenu.Portal>
      </ContextMenu.Root>,
    );

    await userEvent.click(part("o-trigger"), { button: "right" });

    await expect.poll(() => maybePart("o-popup")).not.toBeNull();
    expect(onOpenChange).toHaveBeenCalled();
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);
  });

  it("honours a controlled open prop", async () => {
    const { rerender } = await render(
      <ContextMenu.Root open={false}>
        <ContextMenu.Trigger data-testid="c-trigger">Right click</ContextMenu.Trigger>
        <ContextMenu.Portal>
          <ContextMenu.Positioner>
            <ContextMenu.Popup data-testid="c-popup">
              <ContextMenu.Item>Duplicate</ContextMenu.Item>
            </ContextMenu.Popup>
          </ContextMenu.Positioner>
        </ContextMenu.Portal>
      </ContextMenu.Root>,
    );

    expect(maybePart("c-popup")).toBeNull();

    await rerender(
      <ContextMenu.Root open>
        <ContextMenu.Trigger data-testid="c-trigger">Right click</ContextMenu.Trigger>
        <ContextMenu.Portal>
          <ContextMenu.Positioner>
            <ContextMenu.Popup data-testid="c-popup">
              <ContextMenu.Item>Duplicate</ContextMenu.Item>
            </ContextMenu.Popup>
          </ContextMenu.Positioner>
        </ContextMenu.Portal>
      </ContextMenu.Root>,
    );

    expect(part("c-popup").hasAttribute("data-open")).toBe(true);
    expect(part("c-trigger").hasAttribute("data-popup-open")).toBe(true);
  });

  it("lets a caller className override trigger recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both radius classes on and fails.
    await render(
      <ContextMenu.Root>
        <ContextMenu.Trigger data-testid="w-trigger" className="rounded-lg border border-border">
          Right click
        </ContextMenu.Trigger>
      </ContextMenu.Root>,
    );

    const trigger = part("w-trigger");
    expect(trigger.classList.contains("rounded-lg")).toBe(true);
    expect(trigger.classList.contains("rounded-md")).toBe(false);
    expect(trigger.classList.contains("border-border")).toBe(true);
    expect(trigger.classList.contains("select-none")).toBe(true);
    expect(trigger.classList.contains("data-[popup-open]:ring-2")).toBe(true);
  });

  it("carries the trigger recipe onto another element through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all, and it matters more here than anywhere: a context-menu trigger
    // usually IS the caller's own card or row rather than a wrapper around it.
    await render(
      <ContextMenu.Root>
        <ContextMenu.Trigger data-testid="r-trigger" render={<section />}>
          Right click
        </ContextMenu.Trigger>
      </ContextMenu.Root>,
    );

    const trigger = part("r-trigger");
    expect(trigger.tagName).toBe("SECTION");
    expect(classSet(trigger)).toEqual(TRIGGER_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <ContextMenu.Root open>
        <ContextMenu.Trigger data-testid="project-card" aria-label="Project card">
          Right click
        </ContextMenu.Trigger>
        <ContextMenu.Portal>
          <ContextMenu.Positioner>
            <ContextMenu.Popup data-testid="project-actions" aria-label="Project actions">
              <ContextMenu.Item data-testid="project-delete" disabled>
                Delete
              </ContextMenu.Item>
            </ContextMenu.Popup>
          </ContextMenu.Positioner>
        </ContextMenu.Portal>
      </ContextMenu.Root>,
    );

    expect(part("project-card").getAttribute("aria-label")).toBe("Project card");
    expect(part("project-actions").getAttribute("aria-label")).toBe("Project actions");
    expect(part("project-delete").hasAttribute("data-disabled")).toBe(true);
  });
});
