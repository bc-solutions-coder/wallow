import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it } from "vitest";
import { userEvent } from "vitest/browser";

import { Menu } from "../menu/menu";
import { Menubar } from "./menubar";
import { menubarTriggerRecipe } from "./menubar.styles";

/*
 * Menubar behavioural spec (Wallow-m5aq.3.8), shaped after the Wallow-m5aq.3.1
 * Dialog exemplar and composing the Wallow-m5aq.3.6 Menu component it reuses:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `menubarRecipe` and inspecting its return value: a recipe unit test would
 *      pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. `MENUBAR_CLASSES` and
 *      `MENUBAR_TRIGGER_CLASSES` below are the single source of truth for the two
 *      class lists this component owns.
 *   4. Stories carry the visual coverage (see menubar.stories.tsx); this file is
 *      only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <div role="menubar" id aria-orientation="horizontal"
 *        data-orientation="horizontal" data-modal>          <- Menubar
 *     …and data-has-submenu-open while ANY of its menus is open.
 *
 *     <button role="menuitem" type="button" tabindex="0|-1"    <- Menu.Trigger,
 *             aria-haspopup="menu" aria-expanded="false">         reshaped by the
 *       …one per menu, and the DIRECT children of the bar:         bar's context
 *       `Menu.Root` renders no DOM at all.
 *       While its menu is open it gains data-popup-open, data-pressed,
 *       aria-expanded="true" and aria-controls.
 *
 *   …and, only while that menu is open, portalled onto <body>:
 *   <div data-base-ui-portal>                              <- Menu.Portal
 *     <div role="presentation" data-base-ui-inert
 *          style="position:fixed;inset:0">                 <- Base UI's OWN blocker
 *     <div data-open data-side data-align role="presentation"
 *          style="position:absolute;transform:…;--anchor-width:<trigger width>">
 *                                                          <- Menu.Positioner
 *       <div role="menu" tabindex="-1" data-open           <- Menu.Popup
 *            aria-labelledby="<trigger id>" data-rootownerid="<bar id>">
 *         …then exactly the Menu anatomy, because it IS the Menu component.
 *
 * Six consequences worth knowing before editing this file:
 *
 *   - THE MENUS ARE THE `Menu` COMPONENT, NOT RE-WRAPS. Base UI's menubar subpath
 *     publishes one export — the strip — so everything below a trigger here is
 *     `Menu`'s own wrapper carrying `menu.styles.ts`'s recipes, and the specs
 *     that read those class lists are regression pins on that reuse;
 *   - THE BAR IS A COMPOSITE. Its triggers share ONE tab stop: exactly one of
 *     them has `tabindex="0"` at a time and the arrow keys move it (looping at
 *     the ends; Home and End jump). While a menu is DOWN, those same keys close
 *     it and open the neighbour instead — the behaviour that makes a bar a bar;
 *   - ARROWRIGHT IS OVERLOADED, and the distinction is measured below: on a plain
 *     row it switches to the next MENU, on a `SubmenuTrigger` row it opens that
 *     row's submenu and the bar stays where it is;
 *   - A MODAL MENU ALWAYS RENDERS ONE MORE ELEMENT THAN YOU WROTE: Base UI puts
 *     an unstyleable `<div role="presentation" style="position:fixed;inset:0">`
 *     inside the portal to block outside pointer events. This project loads no
 *     Tailwind, so the popup's `z-50` is inert and the blocker covers it — a
 *     `userEvent.click` on anything INSIDE an open popup hits the blocker and
 *     times out on Playwright's actionability check. Interaction inside a menu
 *     therefore goes through the KEYBOARD here (which a menubar wants anyway) or
 *     a direct `element.click()`. Realistic pointer coverage lives in the stories;
 *   - CLOSING IS ANIMATION-FRAME-DEFERRED and ROVING FOCUS IS ASYNCHRONOUS, the
 *     two Wave-2 timing gotchas: every absence assertion uses
 *     `await expect.poll(...)`, never a bare synchronous read, and so does every
 *     focus assertion;
 *   - FOCUS IS RESTORED TO THE TRIGGER on every close path (Escape, choosing a
 *     row), unlike Context Menu — a menubar trigger is a real `<button>`.
 *
 * THE ONE REAL-POINTER SPEC IS LAST IN THIS FILE, ON PURPOSE. The Playwright
 * mouse position persists from spec to spec within a file, and a menubar opens a
 * neighbouring menu on HOVER once any menu is down; leaving the pointer parked
 * over a trigger would make the keyboard specs below it ambiguous.
 *
 * The pointer ALSO leaks in from other files, over coordinates this fixture
 * happens to reuse — and Chromium re-dispatches hover events when new content
 * mounts under a stationary cursor. A keyboard-opened popup mounting under a
 * parked pointer either hover-highlights the row at that spot (focus lands on
 * the last row, not the first) or, in the frame before the modal blocker
 * mounts, hover-switches to a neighbouring menu. Every keyboard open therefore
 * names its pointer state first: `userEvent.unhover(bar)` moves the pointer to
 * `<body>`, off the bar and off the footprint of every popup.
 */

/**
 * Utilities `Menubar` must render.
 *
 * The strip is a layout element with a chrome of its own: it is the surface the
 * menu names sit on, so unlike the menu card it uses `bg-background` rather than
 * `bg-popover` — a bar belongs to the page, a popup floats above it.
 *
 * Orientation is styled from Base UI's own `data-orientation` attribute instead
 * of a cva variant, so the class list is the SAME in both orientations and only
 * the attribute on the element decides which half applies. `w-fit` keeps the bar
 * hugging its menus; a full-width application bar is `className="w-full"`.
 */
const MENUBAR_CLASSES = [
  "flex",
  "w-fit",
  "gap-1",
  "rounded-md",
  "border",
  "border-border",
  "bg-background",
  "p-1",
  "text-foreground",
  "shadow-sm",
  "data-[orientation=horizontal]:flex-row",
  "data-[orientation=horizontal]:items-center",
  "data-[orientation=vertical]:flex-col",
  "data-[orientation=vertical]:items-stretch",
];

/**
 * What `menubarTriggerRecipe()` adds to a `Menu.Trigger` standing in a bar.
 *
 * Purely additive — none of these conflicts with `menuTriggerRecipe`, so the
 * merged trigger still gets its shape (`inline-flex`, `rounded-md`, `text-sm
 * font-medium`, `transition-colors`, `data-[disabled]:opacity-50`) from `Menu`.
 * The delta is the two things a standalone menu button has no use for: room to
 * breathe inside the strip, and a filled `data-[popup-open]:` state so the menu
 * on screen visibly belongs to its name.
 */
const MENUBAR_TRIGGER_CLASSES = [
  "px-3",
  "py-1.5",
  "outline-none",
  "focus-visible:ring-2",
  "focus-visible:ring-ring",
  "data-[popup-open]:bg-accent",
  "data-[popup-open]:text-accent-foreground",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/**
 * The part carrying `data-testid`, searched across the whole document because the
 * open half of every menu is portalled out of the render container.
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

interface AppMenubarProps {
  /** Lays the bar out as a vertical rail instead of a horizontal strip. */
  readonly orientation?: "horizontal" | "vertical";
  /** Turns off Base UI's modal behaviour for the whole bar. */
  readonly modal?: boolean;
  /** Disables every menu on the bar at once. */
  readonly disabled?: boolean;
}

/**
 * Three menus on one bar — the smallest fixture that can show a bar behaving as a
 * bar, since switching menus needs a neighbour and looping needs a third. The
 * rows carry real text deliberately: this project loads no Tailwind, so a textless
 * row would measure 0x0 and be unreachable to a real pointer.
 */
function AppMenubar({ orientation, modal, disabled }: AppMenubarProps): ReactElement {
  return (
    <Menubar data-testid="bar" orientation={orientation} modal={modal} disabled={disabled}>
      <Menu.Root>
        <Menu.Trigger data-testid="file-trigger" className={menubarTriggerRecipe()}>
          File
        </Menu.Trigger>
        <Menu.Portal data-testid="file-portal">
          <Menu.Positioner data-testid="file-positioner">
            <Menu.Popup data-testid="file-popup">
              <Menu.Item data-testid="file-new">New file</Menu.Item>
              <Menu.Item data-testid="file-open">Open…</Menu.Item>
              <Menu.Separator data-testid="file-separator" />
              <Menu.SubmenuRoot>
                <Menu.SubmenuTrigger data-testid="file-recent">Recent</Menu.SubmenuTrigger>
                <Menu.Portal>
                  <Menu.Positioner data-testid="file-recent-positioner">
                    <Menu.Popup data-testid="file-recent-popup">
                      <Menu.Item data-testid="file-recent-one">wallow.md</Menu.Item>
                    </Menu.Popup>
                  </Menu.Positioner>
                </Menu.Portal>
              </Menu.SubmenuRoot>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>
      <Menu.Root>
        <Menu.Trigger data-testid="edit-trigger" className={menubarTriggerRecipe()}>
          Edit
        </Menu.Trigger>
        <Menu.Portal>
          <Menu.Positioner data-testid="edit-positioner">
            <Menu.Popup data-testid="edit-popup">
              <Menu.Item data-testid="edit-undo">Undo</Menu.Item>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>
      <Menu.Root>
        <Menu.Trigger data-testid="view-trigger" className={menubarTriggerRecipe()}>
          View
        </Menu.Trigger>
        <Menu.Portal>
          <Menu.Positioner>
            <Menu.Popup data-testid="view-popup">
              <Menu.Item data-testid="view-zoom">Zoom in</Menu.Item>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>
    </Menubar>
  );
}

/**
 * Renders the bar and opens a menu the way the keyboard does it: focus the name,
 * press Enter. No pointer is involved, so the open popup's blocker never matters
 * and the mouse stays wherever the previous spec left it.
 */
async function openWithKeyboard(triggerTestId: string, firstRowTestId: string): Promise<void> {
  await render(<AppMenubar />);

  // The pointer must be off the fixture before the popup mounts — see the header.
  await userEvent.unhover(part("bar"));
  part(triggerTestId).focus();
  await userEvent.keyboard("{Enter}");
  await expect.poll(focusedTestId).toBe(firstRowTestId);
}

/** Steps the roving focus one place and waits for it to land, since it moves a tick late. */
async function pressKey(key: string, expectedTestId: string): Promise<void> {
  await userEvent.keyboard(key);
  await expect.poll(focusedTestId).toBe(expectedTestId);
}

describe("Menubar", () => {
  it("renders the strip as a role=menubar composite", async () => {
    // The bar is the only element this component renders. `data-modal` is present
    // by default (Base UI's `modal` defaults to true) and `aria-orientation`
    // mirrors the orientation prop for assistive technology.
    await render(<AppMenubar />);

    const bar = part("bar");
    expect(bar.tagName).toBe("DIV");
    expect(bar.getAttribute("role")).toBe("menubar");
    expect(bar.getAttribute("aria-orientation")).toBe("horizontal");
    expect(bar.getAttribute("data-orientation")).toBe("horizontal");
    expect(bar.hasAttribute("data-modal")).toBe(true);
    expect(bar.hasAttribute("data-has-submenu-open")).toBe(false);
  });

  it("answers the orientation and modal props on the bar element", async () => {
    // Both are read back by the recipe and by assistive technology, so both have
    // to survive the wrapper. `data-orientation` is what the recipe's
    // `data-[orientation=vertical]:` half hangs off.
    await render(<AppMenubar orientation="vertical" modal={false} />);

    const bar = part("bar");
    expect(bar.getAttribute("data-orientation")).toBe("vertical");
    expect(bar.getAttribute("aria-orientation")).toBe("vertical");
    expect(bar.hasAttribute("data-modal")).toBe(false);
  });

  it("reshapes each menu's trigger into a menuitem sharing one tab stop", async () => {
    // The whole reason a menubar exists as a component: `Menu.Root` renders no DOM,
    // so the triggers are the bar's own children, and Base UI's MenubarContext
    // turns each of them from a standalone button into a roving `role="menuitem"`.
    // Tab therefore enters and leaves the bar rather than walking three buttons.
    await render(<AppMenubar />);

    expect(part("bar").children.length).toBe(3);
    expect(part("file-trigger").getAttribute("role")).toBe("menuitem");
    expect(part("file-trigger").getAttribute("aria-haspopup")).toBe("menu");
    expect(part("file-trigger").getAttribute("aria-expanded")).toBe("false");
    expect(part("file-trigger").getAttribute("tabindex")).toBe("0");
    expect(part("edit-trigger").getAttribute("tabindex")).toBe("-1");
    expect(part("view-trigger").getAttribute("tabindex")).toBe("-1");
  });

  it("keeps every portalled part out of the DOM while the bar is closed", async () => {
    await render(<AppMenubar />);

    expect(maybePart("file-portal")).toBeNull();
    expect(maybePart("file-positioner")).toBeNull();
    expect(maybePart("file-popup")).toBeNull();
    expect(maybePart("edit-popup")).toBeNull();
  });

  it("opens the focused menu with Enter and lands on its first row", async () => {
    await openWithKeyboard("file-trigger", "file-new");

    const popup = part("file-popup");
    expect(popup.getAttribute("role")).toBe("menu");
    expect(popup.hasAttribute("data-open")).toBe(true);
    expect(part("file-trigger").getAttribute("aria-expanded")).toBe("true");
    expect(part("file-trigger").hasAttribute("data-popup-open")).toBe(true);
    expect(part("file-trigger").hasAttribute("data-pressed")).toBe(true);
    expect(part("file-trigger").getAttribute("aria-controls")).toBe(popup.id);
  });

  it("opens the focused menu with ArrowDown as well", async () => {
    // The menubar keyboard convention: down opens the menu under the name. It is
    // the same key that would rove focus in a VERTICAL bar, which is why the
    // orientation prop reaches Base UI rather than only the recipe.
    await render(<AppMenubar />);

    // The pointer must be off the fixture before the popup mounts — see the header.
    await userEvent.unhover(part("bar"));
    part("file-trigger").focus();
    await pressKey("{ArrowDown}", "file-new");

    expect(part("file-popup").hasAttribute("data-open")).toBe(true);
  });

  it("names the open popup by its trigger and stamps the bar's id on it", async () => {
    // How a screen reader ties the card to the name that opened it, and how Base
    // UI ties it back to this particular bar — the second one is what lets two
    // menubars on a page keep their menus apart.
    await openWithKeyboard("file-trigger", "file-new");

    expect(part("file-popup").getAttribute("aria-labelledby")).toBe(part("file-trigger").id);
    expect(part("file-popup").getAttribute("data-rootownerid")).toBe(part("bar").id);
  });

  it("marks the bar while one of its menus is open and clears it on close", async () => {
    // `data-has-submenu-open` is Base UI's name for it, and the name undersells
    // what it means: it is present whenever ANY menu of the bar is down, nested or
    // not (measured). It is the only hook a fork has for restyling the whole strip
    // while it is in use.
    await openWithKeyboard("file-trigger", "file-new");

    expect(part("bar").hasAttribute("data-has-submenu-open")).toBe(true);

    await userEvent.keyboard("{Escape}");

    await expect.poll(() => maybePart("file-popup")).toBeNull();
    expect(part("bar").hasAttribute("data-has-submenu-open")).toBe(false);
  });

  it("anchors the popup to the trigger box", async () => {
    // The measured contrast with Context Menu, which is anchored to the cursor and
    // reports a zero-size anchor: here the positioner is absolute inside the page
    // and `--anchor-width` is the width of the menu name it hangs under.
    await openWithKeyboard("file-trigger", "file-new");

    const positioner = part("file-positioner");
    const triggerWidth = part("file-trigger").getBoundingClientRect().width;
    expect(positioner.style.position).toBe("absolute");
    expect(positioner.style.transform).not.toBe("");
    expect(Number.parseFloat(positioner.style.getPropertyValue("--anchor-width"))).toBeCloseTo(
      triggerWidth,
      0,
    );
  });

  it("roves focus along the bar with the arrow keys while every menu is closed", async () => {
    // A closed bar behaves like a toolbar: the arrows move the single tab stop and
    // open nothing. The tabindex flip is the assertion that matters — it is what
    // keeps the bar one Tab stop rather than three.
    await render(<AppMenubar />);

    part("file-trigger").focus();
    await pressKey("{ArrowRight}", "edit-trigger");

    expect(part("file-trigger").getAttribute("tabindex")).toBe("-1");
    expect(part("edit-trigger").getAttribute("tabindex")).toBe("0");
    expect(maybePart("edit-popup")).toBeNull();

    await pressKey("{ArrowLeft}", "file-trigger");
    expect(maybePart("file-popup")).toBeNull();
  });

  it("loops the roving focus past both ends and jumps with Home and End", async () => {
    await render(<AppMenubar />);

    part("view-trigger").focus();
    await pressKey("{ArrowRight}", "file-trigger");
    await pressKey("{ArrowLeft}", "view-trigger");
    await pressKey("{Home}", "file-trigger");
    await pressKey("{End}", "view-trigger");
  });

  it("switches the open menu along the bar with the arrow keys", async () => {
    // The behaviour that makes a bar a bar rather than three unrelated dropdowns:
    // with a menu down, the arrows no longer rove focus, they move the MENU. The
    // previous popup unmounts and the neighbour's opens with focus already in it.
    await openWithKeyboard("file-trigger", "file-new");

    await userEvent.keyboard("{ArrowRight}");

    await expect.poll(focusedTestId).toBe("edit-popup");
    await expect.poll(() => maybePart("file-popup")).toBeNull();
    expect(part("edit-trigger").getAttribute("aria-expanded")).toBe("true");
    expect(part("file-trigger").hasAttribute("data-popup-open")).toBe(false);

    await userEvent.keyboard("{ArrowLeft}");

    await expect.poll(focusedTestId).toBe("file-popup");
    await expect.poll(() => maybePart("edit-popup")).toBeNull();
  });

  it("opens a submenu with ArrowRight instead of switching menus", async () => {
    // ArrowRight is overloaded, and this is the half that would break first: on a
    // plain row it moves along the BAR, on a `SubmenuTrigger` row it opens that
    // row's own menu and the bar stays where it is.
    await openWithKeyboard("file-trigger", "file-new");

    await pressKey("{End}", "file-recent");
    await userEvent.keyboard("{ArrowRight}");

    await expect.poll(focusedTestId).toBe("file-recent-one");
    expect(part("file-recent-popup").hasAttribute("data-nested")).toBe(true);
    expect(part("file-recent").getAttribute("aria-expanded")).toBe("true");
    // Neither neighbour opened, and the parent menu is still on screen.
    expect(maybePart("edit-popup")).toBeNull();
    expect(part("file-popup").hasAttribute("data-open")).toBe(true);
  });

  it("closes on Escape and returns focus to the trigger", async () => {
    // The sharpest divergence from Context Menu, whose non-focusable `<div>`
    // trigger leaves focus on `<body>`: a menubar trigger is a real `<button>`, so
    // the keyboard user is put back where they started and Tab still works.
    await openWithKeyboard("file-trigger", "file-new");

    await userEvent.keyboard("{Escape}");

    await expect.poll(() => maybePart("file-popup")).toBeNull();
    await expect.poll(focusedTestId).toBe("file-trigger");
    expect(part("file-trigger").hasAttribute("data-popup-open")).toBe(false);
  });

  it("closes and restores focus when a row is chosen", async () => {
    await openWithKeyboard("file-trigger", "file-new");

    // A direct DOM click rather than `userEvent.click`: Base UI's own fixed pointer
    // blocker covers the unstyled popup in this project, so Playwright's
    // actionability check would never resolve. See the header.
    part("file-open").click();

    await expect.poll(() => maybePart("file-popup")).toBeNull();
    await expect.poll(focusedTestId).toBe("file-trigger");
  });

  it("disables every menu on the bar at once", async () => {
    // One prop on the strip, not one per menu — and it reaches the triggers through
    // the same context that reshapes them, so it must survive the wrapper.
    await render(<AppMenubar disabled />);

    const trigger = part("file-trigger");
    expect(trigger.hasAttribute("data-disabled")).toBe(true);
    expect(trigger.getAttribute("aria-disabled")).toBe("true");
    expect((trigger as HTMLButtonElement).disabled).toBe(true);

    trigger.click();

    expect(maybePart("file-popup")).toBeNull();
  });

  it("dresses the menus in the Menu component's own recipes", async () => {
    // Not a duplicate of menu.test.tsx: that file proves the class lists are right,
    // this one proves a menubar's menus get THOSE lists rather than a second set
    // minted here. Base UI publishes no menubar dropdown parts to mirror, so a
    // re-wrap would be pure invention — and this fails the moment one appears.
    await openWithKeyboard("file-trigger", "file-new");

    expect(part("file-popup").classList.contains("bg-popover")).toBe(true);
    expect(part("file-popup").classList.contains("min-w-32")).toBe(true);
    expect(part("file-popup").classList.contains("p-1")).toBe(true);
    expect(part("file-positioner").classList.contains("z-50")).toBe(true);
    expect(part("file-new").classList.contains("px-2")).toBe(true);
    expect(part("file-new").classList.contains("data-[highlighted]:bg-accent")).toBe(true);
    expect(part("file-separator").classList.contains("bg-border")).toBe(true);
  });

  it("renders the bar with its recipe", async () => {
    await render(<AppMenubar />);

    expect(classSet(part("bar"))).toEqual(MENUBAR_CLASSES.toSorted());
  });

  it("keeps the same class list in a vertical bar", async () => {
    // The consequence of styling orientation from `data-orientation` rather than a
    // cva variant: the class list never changes, only the attribute the modifiers
    // hang off does. A caller cannot get the painting and the behaviour out of step.
    await render(<AppMenubar orientation="vertical" />);

    expect(classSet(part("bar"))).toEqual(MENUBAR_CLASSES.toSorted());
  });

  it("adds the menubar delta on top of the Menu trigger recipe", async () => {
    // `menubarTriggerRecipe()` is a class list a caller hands to `Menu.Trigger`
    // rather than a second wrapper around the same Base UI part. Both halves have
    // to survive the merge: the delta below, and the shape that still comes from
    // `menuTriggerRecipe`.
    await render(<AppMenubar />);

    const trigger = part("file-trigger");
    for (const utility of MENUBAR_TRIGGER_CLASSES) {
      expect(trigger.classList.contains(utility), `trigger is missing ${utility}`).toBe(true);
    }
    expect(trigger.classList.contains("inline-flex")).toBe(true);
    expect(trigger.classList.contains("text-sm")).toBe(true);
    expect(trigger.classList.contains("font-medium")).toBe(true);
    expect(trigger.classList.contains("data-[disabled]:opacity-50")).toBe(true);
  });

  it("lets a caller className override bar recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both width classes on and fails.
    await render(
      <Menubar data-testid="wide-bar" className="w-full rounded-none">
        <Menu.Root>
          <Menu.Trigger data-testid="wide-trigger">File</Menu.Trigger>
        </Menu.Root>
      </Menubar>,
    );

    const bar = part("wide-bar");
    expect(bar.classList.contains("w-full")).toBe(true);
    expect(bar.classList.contains("w-fit")).toBe(false);
    expect(bar.classList.contains("rounded-none")).toBe(true);
    expect(bar.classList.contains("rounded-md")).toBe(false);
    expect(bar.classList.contains("border-border")).toBe(true);
    expect(bar.classList.contains("data-[orientation=vertical]:flex-col")).toBe(true);
  });

  it("carries the bar recipe onto another element through the render prop", async () => {
    // An application menubar is usually a landmark rather than a bare div, and Base
    // UI's `render` prop is how the catalog lets a caller say so without losing the
    // recipe or the composite behaviour.
    await render(
      <Menubar data-testid="nav-bar" render={<nav aria-label="Main" />}>
        <Menu.Root>
          <Menu.Trigger data-testid="nav-trigger">File</Menu.Trigger>
        </Menu.Root>
      </Menubar>,
    );

    const bar = part("nav-bar");
    expect(bar.tagName).toBe("NAV");
    expect(bar.getAttribute("aria-label")).toBe("Main");
    expect(bar.getAttribute("role")).toBe("menubar");
    expect(classSet(bar)).toEqual(MENUBAR_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <Menubar data-testid="app-bar" id="app-menubar" aria-label="Application">
        <Menu.Root>
          <Menu.Trigger data-testid="app-file">File</Menu.Trigger>
        </Menu.Root>
      </Menubar>,
    );

    const bar = part("app-bar");
    expect(bar.getAttribute("id")).toBe("app-menubar");
    expect(bar.getAttribute("aria-label")).toBe("Application");
    // Base UI labels the popup by the trigger, so the trigger's id has to be the
    // generated one even when the bar's is the caller's.
    expect(part("app-file").id).not.toBe("");
  });

  /*
   * LAST ON PURPOSE — see the header. This is the file's only real-pointer spec,
   * and Playwright's mouse position survives into the specs that follow it.
   */
  it("opens a menu when its name is clicked", async () => {
    await render(<AppMenubar />);

    // Clicking the TRIGGER is always safe: it is outside the portal, and Base UI's
    // pointer blocker does not exist until a menu is open.
    await userEvent.click(part("file-trigger"));

    // A pointer-opened menu focuses the POPUP, not the first row — the keyboard
    // paths above focus the row, and both are Base UI's own behaviour.
    await expect.poll(focusedTestId).toBe("file-popup");
    expect(part("file-popup").hasAttribute("data-open")).toBe(true);
    expect(part("file-trigger").hasAttribute("data-popup-open")).toBe(true);
  });
});
