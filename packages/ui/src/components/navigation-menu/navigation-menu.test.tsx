import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { NavigationMenu } from "./navigation-menu";

/*
 * Navigation Menu behavioural spec (Wallow-m5aq.3.9), shaped after the
 * Wallow-m5aq.3.1 Dialog exemplar and its Wave-2 siblings:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `navigationMenuLinkRecipe` and inspecting its return value: a recipe unit
 *      test would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into navigation-menu.styles.ts.
 *   4. Stories carry the visual coverage (see navigation-menu.stories.tsx); this
 *      file is only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed —
 * four throwaway probe rounds, written, run and deleted):
 *
 *   <nav data-testid>                                    <- NavigationMenu.Root
 *     …gains data-open while any item is open. A NESTED Root renders
 *     <div data-nested> instead of <nav> (measured).
 *     <ul>                                               <- NavigationMenu.List
 *       …also gains data-open
 *       <li>                                             <- NavigationMenu.Item
 *         <button type="button" aria-expanded
 *                 data-base-ui-navigation-menu-trigger>  <- NavigationMenu.Trigger
 *           …gains data-popup-open, data-pressed and aria-controls="<popup id>"
 *           <span aria-hidden>                           <- NavigationMenu.Icon
 *             …gains data-popup-open in step with its trigger
 *         …while THIS item is open Base UI injects two <span data-base-ui-focus-guard>
 *         and a <span aria-owns="<viewport id>"> as siblings of the trigger
 *         <a>                                            <- NavigationMenu.Link
 *           …an Item may hold a Link directly instead of a Trigger + Content
 *
 *   …and, only while open, portalled onto <body>:
 *   <div data-base-ui-portal>                            <- NavigationMenu.Portal
 *     <div data-open role="presentation" style="user-select:none">
 *                                                        <- NavigationMenu.Backdrop
 *     <div data-open data-side data-align role="presentation"
 *          style="position:absolute;left;top;--available-*;--anchor-*">
 *                                                        <- NavigationMenu.Positioner
 *       <nav data-open data-side data-align tabindex="-1"
 *            style="--popup-width;--popup-height">       <- NavigationMenu.Popup
 *         <div data-open data-side data-align aria-hidden
 *              style="position:absolute;left:…">         <- NavigationMenu.Arrow
 *         <span data-base-ui-focus-guard>
 *         <div>                                          <- NavigationMenu.Viewport
 *           <div data-open>            …the ACTIVE item's Content, MOVED here
 *                                                        <- NavigationMenu.Content
 *             <a data-active aria-current="page">        <- NavigationMenu.Link active
 *         <span data-base-ui-focus-guard>
 *
 * NINE consequences worth knowing before editing this file. The first five are
 * the Wave-2 gotchas the Dialog exemplar pinned; the last four are specific to
 * this component and were measured here.
 *
 *   (1) PORTAL. Nothing under NavigationMenu.Portal is in the DOM while closed —
 *       these are absent elements, not hidden ones. Every open-state query goes
 *       through `document.body`, NEVER through `render`'s `container`. Hence the
 *       `part()`/`maybePart()` helpers below.
 *
 *   (2) THERE IS NO BASE UI POINTER BLOCKER HERE — and that is WORSE, not
 *       better. A navigation menu is NOT modal, so Base UI renders no
 *       `<div style="position:fixed;inset:0">` blocker (measured: the only
 *       `role="presentation"` elements in the portal are the Backdrop and the
 *       Positioner). What DOES intercept pointers is the open POPUP ITSELF: this
 *       project loads no Tailwind, so the popup's `z-50` does nothing, and the
 *       positioner still parks the panel on top of the trigger list at
 *       `top: 45px`. A `userEvent.click` on a SECOND trigger while the menu is
 *       open therefore times out after ~15s on Playwright's actionability check
 *       ("<a data-testid=…> from <div data-base-ui-portal> subtree intercepts
 *       pointer events"). Trigger-switching coverage goes through the KEYBOARD
 *       or a direct `element.click()` here; realistic pointer coverage lives in
 *       navigation-menu.stories.tsx.
 *
 *   (3) CLOSING IS ANIMATION-FRAME-DEFERRED on at least the outside-press path
 *       (measured: the popup is still in the DOM synchronously after a
 *       dispatched outside press and gone a frame later). Escape and a second
 *       trigger press happened to unmount synchronously, but every absence
 *       assertion here still uses `await expect.poll(...)` — the exemplar's
 *       standing instruction, because which paths defer is not a contract.
 *
 *   (4) `data-starting-style` / `data-ending-style` only exist DURING a
 *       transition, so no spec asserts them on an element. They are pinned as
 *       `data-[starting-style]:` / `data-[ending-style]:` modifiers inside the
 *       recipe class sets instead, which is what the catalog actually owns.
 *
 *   (5) OPENING VIA `defaultValue` DOES NOT MOVE FOCUS (measured:
 *       `document.activeElement` stays on `<body>`). Every focus assertion opens
 *       through a real trigger interaction. The flip side is the useful half:
 *       `defaultValue` mounts the ENTIRE portal half with no pointer at all,
 *       which is how every recipe/markup spec below opens the menu.
 *
 *   (6) *** NEVER `element.click()` A LINK WITH A REAL href IN THIS PROJECT. ***
 *       `NavigationMenu.Link` renders a genuine `<a>`, and a real click on
 *       `href="https://example.com/one"` NAVIGATES the vitest iframe, which
 *       kills the entire run with "Cannot connect to the iframe. Did you change
 *       the location…" — not one failing spec, the whole file. Cost a probe
 *       round. Links in this file either carry no `href` at all or a `#hash`
 *       (measured safe: the hash changes, the iframe survives). The same rule
 *       applies to the stories.
 *
 *   (7) THE POPUP IS SHARED AND THE CONTENT MOVES INTO IT. Unlike Menu, where
 *       each trigger owns a popup, one Positioner/Popup/Viewport serves the
 *       whole menu and the ACTIVE item's `Content` is relocated into the
 *       Viewport. Switching triggers unmounts the old Content and mounts the new
 *       one inside the SAME popup, which is why `Content` is the part carrying
 *       the panel's padding while `Popup` carries the card's paint.
 *
 *   (8) THE MENU OPENS ON HOVER, with a 50ms `delay` (measured — there is no
 *       `openOnHover` prop to turn it off). Combined with the standing
 *       "Playwright's mouse position persists across specs in a file" memory,
 *       this means the pointer-driven specs are LAST in this file and the single
 *       closed-state spec is FIRST. A fresh render under a parked mouse was
 *       measured NOT to auto-open (Playwright fires no new hover without
 *       movement), but the ordering costs nothing and removes the class of flake
 *       entirely.
 *
 *   (9) ORIENTATION IS INVISIBLE IN THE MARKUP. `orientation="vertical"` puts NO
 *       `data-orientation` on the Root or the List (measured); it only swaps the
 *       arrow-key axis — horizontal roves the triggers with ArrowLeft/Right and
 *       enters the panel with ArrowDown, vertical roves with ArrowUp/Down and
 *       enters with ArrowRight. So orientation is pinned BEHAVIOURALLY below,
 *       not by an attribute, and no recipe may key off it.
 */

/**
 * Utilities `NavigationMenu.Root` must render. `min-w-0` is the whole reason
 * this recipe is not empty: without it a `w-16` icon rail is blown open by its
 * own labels, which is the bug the Phase-4 sidebar sweep exists to fix. `flex`
 * gives the landmark a direction the caller flips with `flex-col`.
 */
const ROOT_CLASSES = ["flex", "min-w-0"];

/**
 * Utilities `NavigationMenu.List` must render. `m-0 list-none p-0` is a real
 * reset, not decoration: this part is a genuine `<ul>`, and a browser's default
 * `padding-inline-start` of 40px would push every row off a 64px icon rail
 * before any of the catalog's own spacing applied.
 */
const LIST_CLASSES = ["m-0", "flex", "min-w-0", "list-none", "gap-1", "p-0"];

/**
 * Utilities `NavigationMenu.Item` must render. It carries its own marker reset
 * rather than relying on the List's, because an Item is legitimately rendered
 * outside a List through the `render` prop.
 */
const ITEM_CLASSES = ["min-w-0", "list-none"];

/**
 * Utilities `NavigationMenu.Trigger` must render.
 *
 * This DELIBERATELY diverges from `dialogTriggerRecipe`/`menuTriggerRecipe`,
 * which are colourless because a dialog or menu trigger is routinely composed
 * onto a real `Button`. A navigation trigger is not a button in a toolbar, it is
 * a NAV ROW that happens to open a panel, and it has to sit flush beside the
 * `Link` rows in the same list — so it takes the shared row shape and states its
 * hover/open colour, exactly as the target consumer (`DashboardNav`) hand-rolls
 * today. A caller who does want it colourless overrides through `className`,
 * which is the contract the override spec below pins.
 */
const TRIGGER_CLASSES = [
  "flex",
  "min-w-0",
  "items-center",
  "gap-3",
  "rounded-md",
  "px-3",
  "py-2",
  "text-sm",
  "font-medium",
  "whitespace-nowrap",
  "outline-none",
  "transition-colors",
  "hover:bg-accent",
  "hover:text-accent-foreground",
  "data-[popup-open]:bg-accent",
  "data-[popup-open]:text-accent-foreground",
  // `aria-disabled:`, NOT `data-[disabled]:`. Measured: a disabled
  // NavigationMenu.Trigger renders `aria-disabled="true" tabindex="0"` and gets
  // NEITHER a `disabled` attribute nor a `data-disabled` one — it stays
  // focusable so a keyboard user can still reach and read it. A
  // `data-[disabled]:` modifier copied from the other triggers in this catalog
  // would silently never match; the passthrough spec below pins the attribute
  // this depends on.
  "aria-disabled:opacity-50",
];

/**
 * Utilities `NavigationMenu.Icon` must render — the chevron that says the row
 * opens a panel. `ml-auto` parks it at the end of the row whatever the label
 * length, and the rotation is expressed against Base UI's measured
 * `data-popup-open`. Per the Popover ruling (Wallow-m5aq.3.3 gotcha 9), a
 * rotation is asserted as its CLASS only — Tailwind v4 emits `rotate` as its own
 * CSS property, so `transform` is never the thing to look at.
 */
const ICON_CLASSES = [
  "ml-auto",
  "flex",
  "size-4",
  "shrink-0",
  "items-center",
  "justify-center",
  "transition-transform",
  "duration-150",
  "data-[popup-open]:rotate-180",
];

/**
 * Utilities `NavigationMenu.Content` must render. The panel's PADDING lives here
 * rather than on the popup, because one popup is shared by every item (see
 * consequence 7) while each item's content is its own panel.
 */
const CONTENT_CLASSES = ["flex", "min-w-0", "flex-col", "gap-1", "p-2"];

/**
 * Utilities `NavigationMenu.Link` must render — the same row shape as the
 * trigger, so a list of links and a list of triggers line up. Base UI puts
 * `data-active` and `aria-current="page"` on a link marked `active` (measured),
 * so the current-page treatment is a `data-[active]:` modifier and never a cva
 * variant.
 */
const LINK_CLASSES = [
  "flex",
  "min-w-0",
  "items-center",
  "gap-3",
  "rounded-md",
  "px-3",
  "py-2",
  "text-sm",
  "font-medium",
  "whitespace-nowrap",
  "text-foreground",
  "no-underline",
  "outline-none",
  "transition-colors",
  "hover:bg-accent",
  "hover:text-accent-foreground",
  "data-[active]:bg-accent",
  "data-[active]:text-accent-foreground",
];

/**
 * Utilities `NavigationMenu.Backdrop` must render. Measured: Base UI gives this
 * element no inline positioning at all (only `user-select`), so covering the
 * window is entirely the recipe's job — the same finding as `menuBackdropRecipe`.
 *
 * It is deliberately NOT a scrim. A desktop navigation bar must not dim the page
 * behind its own dropdown. The mobile-overlay presentation the Phase-4 sweep
 * needs adds `bg-foreground/50` through `className`, which the MobileOverlay
 * story shows.
 */
const BACKDROP_CLASSES = ["fixed", "inset-0"];

/**
 * Utilities `NavigationMenu.Positioner` must render. Base UI owns this element's
 * inline `position`/`left`/`top`, so the recipe may only add stacking and focus
 * concerns — the same rule as `selectPositionerRecipe` and `menuPositionerRecipe`,
 * and the opposite of `dialogPopupRecipe`, which owns its own centring.
 */
const POSITIONER_CLASSES = ["z-50", "outline-none"];

/**
 * Utilities `NavigationMenu.Popup` must render — the shared card every item's
 * panel appears inside. `relative` is load-bearing: Base UI gives
 * `NavigationMenu.Arrow` an inline `position: absolute` and `left` but no `top`,
 * so the popup has to be the arrow's containing block. No padding here; the
 * Content owns that (consequence 7).
 */
const POPUP_CLASSES = [
  "relative",
  "rounded-md",
  "border",
  "border-border",
  "bg-popover",
  "text-popover-foreground",
  "shadow-md",
  "outline-none",
  "transition-all",
  "duration-150",
  "data-[starting-style]:scale-95",
  "data-[starting-style]:opacity-0",
  "data-[ending-style]:scale-95",
  "data-[ending-style]:opacity-0",
];

/**
 * Utilities `NavigationMenu.Arrow` must render. Identical to `menuArrowRecipe`
 * because both parts run on Base UI's one `useAnchorPositioning` engine and
 * share its `Side` vocabulary; `bottom` and `inline-end` were observed directly
 * here, `top` and `inline-start` come from that shared type.
 */
const ARROW_CLASSES = [
  "size-2.5",
  "rotate-45",
  "rounded-sm",
  "border",
  "border-border",
  "bg-popover",
  "data-[side=bottom]:-top-1",
  "data-[side=top]:-bottom-1",
  "data-[side=inline-start]:-right-1",
  "data-[side=inline-end]:-left-1",
];

/**
 * Utilities `NavigationMenu.Viewport` must render: the clipping a panel
 * cross-fade needs when the active item changes inside one popup.
 */
const VIEWPORT_CLASSES = ["relative", "overflow-hidden"];

/**
 * Every member `@base-ui/react/navigation-menu` publishes on its namespace,
 * sorted. Thirteen — note there is NO `Handle`/`createHandle` pair here (unlike
 * Dialog and Menu) and no `Separator`.
 */
const BASE_UI_PART_NAMES = [
  "Arrow",
  "Backdrop",
  "Content",
  "Icon",
  "Item",
  "Link",
  "List",
  "Popup",
  "Portal",
  "Positioner",
  "Root",
  "Trigger",
  "Viewport",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/**
 * The part carrying `data-testid`, searched across the whole document because
 * the open half of a navigation menu is portalled out of the render container.
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

interface SiteNavProps {
  /** Opens one item on first render WITHOUT any pointer input — see consequence 5. */
  readonly defaultValue?: string;
  /** Drives the open item from outside, for the controlled spec. */
  readonly value?: string | null;
  /** Vertical is the sidebar-rail axis the Phase-4 sweep needs. */
  readonly orientation?: "horizontal" | "vertical";
  /** Called with the item value Base UI wants open. */
  readonly onValueChange?: (value: string | null) => void;
}

/**
 * Every part at once, so one fixture can carry the whole anatomy. Note the third
 * item holds a bare `Link` rather than a Trigger + Content — an Item does not
 * have to open a panel, and the flat row is what a real sidebar is mostly made
 * of. Every `href` is a `#hash` for the reason in consequence 6.
 */
function SiteNav(props: SiteNavProps): ReactElement {
  return (
    <NavigationMenu.Root
      data-testid="n-root"
      defaultValue={props.defaultValue}
      value={props.value}
      orientation={props.orientation}
      onValueChange={props.onValueChange}
    >
      <NavigationMenu.List data-testid="n-list">
        <NavigationMenu.Item data-testid="n-item-products" value="products">
          <NavigationMenu.Trigger data-testid="n-trigger-products">
            Products
            <NavigationMenu.Icon data-testid="n-icon-products">v</NavigationMenu.Icon>
          </NavigationMenu.Trigger>
          <NavigationMenu.Content data-testid="n-content-products">
            <NavigationMenu.Link data-testid="n-link-apps" href="#apps">
              Apps
            </NavigationMenu.Link>
            <NavigationMenu.Link data-testid="n-link-settings" href="#settings" active>
              Settings
            </NavigationMenu.Link>
          </NavigationMenu.Content>
        </NavigationMenu.Item>
        <NavigationMenu.Item data-testid="n-item-company" value="company">
          <NavigationMenu.Trigger data-testid="n-trigger-company">Company</NavigationMenu.Trigger>
          <NavigationMenu.Content data-testid="n-content-company">
            <NavigationMenu.Link data-testid="n-link-about" href="#about">
              About
            </NavigationMenu.Link>
          </NavigationMenu.Content>
        </NavigationMenu.Item>
        <NavigationMenu.Item data-testid="n-item-inquiries">
          <NavigationMenu.Link data-testid="n-link-inquiries" href="#inquiries">
            Inquiries
          </NavigationMenu.Link>
        </NavigationMenu.Item>
      </NavigationMenu.List>
      <NavigationMenu.Portal data-testid="n-portal">
        <NavigationMenu.Backdrop data-testid="n-backdrop" />
        <NavigationMenu.Positioner data-testid="n-positioner" sideOffset={8}>
          <NavigationMenu.Popup data-testid="n-popup">
            <NavigationMenu.Arrow data-testid="n-arrow" />
            <NavigationMenu.Viewport data-testid="n-viewport" />
          </NavigationMenu.Popup>
        </NavigationMenu.Positioner>
      </NavigationMenu.Portal>
    </NavigationMenu.Root>
  );
}

describe("NavigationMenu", () => {
  it("exposes exactly Base UI's namespace members on one namespace object", () => {
    // The catalog-wide multi-part convention: keys mirror Base UI 1:1, so a
    // caller who knows the Base UI docs already knows this API. A key added here
    // that Base UI does not have (or a missing one) fails.
    expect(Object.keys(NavigationMenu).toSorted()).toEqual(BASE_UI_PART_NAMES);
  });

  /*
   * ---------------------------------------------------------------------------
   * CLOSED STATE. This spec is FIRST on purpose: it is the only one that asserts
   * "nothing is open", and the menu opens on hover (consequence 8), so it runs
   * before any spec has moved Playwright's mouse.
   * ---------------------------------------------------------------------------
   */

  it("keeps every portalled part out of the DOM while closed", async () => {
    await render(<SiteNav />);

    expect(maybePart("n-portal")).toBeNull();
    expect(maybePart("n-backdrop")).toBeNull();
    expect(maybePart("n-positioner")).toBeNull();
    expect(maybePart("n-popup")).toBeNull();
    expect(maybePart("n-arrow")).toBeNull();
    expect(maybePart("n-viewport")).toBeNull();
    // A closed item's Content is absent too — it is not merely hidden.
    expect(maybePart("n-content-products")).toBeNull();
    expect(maybePart("n-link-apps")).toBeNull();
    // The list half is always present; only the panel half is portalled.
    expect(part("n-root").hasAttribute("data-open")).toBe(false);
    expect(part("n-trigger-products").getAttribute("aria-expanded")).toBe("false");
  });

  it("renders each part as the element its semantics promise", async () => {
    await render(<SiteNav />);

    expect(part("n-root").tagName).toBe("NAV");
    expect(part("n-list").tagName).toBe("UL");
    expect(part("n-item-products").tagName).toBe("LI");
    expect(part("n-trigger-products").tagName).toBe("BUTTON");
    expect(part("n-trigger-products").getAttribute("type")).toBe("button");
    expect(part("n-icon-products").tagName).toBe("SPAN");
    expect(part("n-icon-products").getAttribute("aria-hidden")).toBe("true");
    expect(part("n-link-inquiries").tagName).toBe("A");
  });

  /*
   * ---------------------------------------------------------------------------
   * OPEN STATE, opened through `defaultValue` so no pointer is involved
   * (consequence 5). Everything about markup and recipes is asserted here.
   * ---------------------------------------------------------------------------
   */

  it("mounts the whole portalled half when an item is open", async () => {
    await render(<SiteNav defaultValue="products" />);

    expect(part("n-root").hasAttribute("data-open")).toBe(true);
    expect(part("n-list").hasAttribute("data-open")).toBe(true);
    expect(part("n-backdrop").hasAttribute("data-open")).toBe(true);
    expect(part("n-positioner").hasAttribute("data-open")).toBe(true);
    expect(part("n-popup").hasAttribute("data-open")).toBe(true);
    // The popup is a `<nav>`, not a `role="menu"` — a navigation menu is a
    // landmark full of links, not a command menu.
    expect(part("n-popup").tagName).toBe("NAV");
    expect(part("n-arrow").getAttribute("aria-hidden")).toBe("true");
  });

  it("wires the open trigger to the popup and lights its icon", async () => {
    await render(<SiteNav defaultValue="products" />);

    const trigger = part("n-trigger-products");
    expect(trigger.getAttribute("aria-expanded")).toBe("true");
    expect(trigger.hasAttribute("data-popup-open")).toBe(true);
    expect(trigger.getAttribute("aria-controls")).toBe(part("n-popup").id);
    // The icon follows its own trigger's state, which is what lets the chevron
    // rotate from a `data-[popup-open]:` modifier rather than a React prop.
    expect(part("n-icon-products").hasAttribute("data-popup-open")).toBe(true);
    // The other trigger is untouched.
    expect(part("n-trigger-company").getAttribute("aria-expanded")).toBe("false");
    expect(part("n-trigger-company").hasAttribute("data-popup-open")).toBe(false);
  });

  it("moves the active item's content into the shared viewport", async () => {
    // Consequence 7, and the single biggest structural difference from Menu: the
    // Content is authored inside its Item but ends up inside the popup.
    await render(<SiteNav defaultValue="products" />);

    const content = part("n-content-products");
    expect(content.hasAttribute("data-open")).toBe(true);
    expect(part("n-viewport").contains(content)).toBe(true);
    expect(part("n-item-products").contains(content)).toBe(false);
    // Only the ACTIVE item's content exists.
    expect(maybePart("n-content-company")).toBeNull();
  });

  it("marks a link that names itself the current page", async () => {
    await render(<SiteNav defaultValue="products" />);

    const active = part("n-link-settings");
    expect(active.hasAttribute("data-active")).toBe(true);
    expect(active.getAttribute("aria-current")).toBe("page");

    const inactive = part("n-link-apps");
    expect(inactive.hasAttribute("data-active")).toBe(false);
    expect(inactive.hasAttribute("aria-current")).toBe(false);
  });

  it("renders the list half with its recipes", async () => {
    await render(<SiteNav defaultValue="products" />);

    expect(classSet(part("n-root"))).toEqual(ROOT_CLASSES.toSorted());
    expect(classSet(part("n-list"))).toEqual(LIST_CLASSES.toSorted());
    expect(classSet(part("n-item-products"))).toEqual(ITEM_CLASSES.toSorted());
    expect(classSet(part("n-trigger-products"))).toEqual(TRIGGER_CLASSES.toSorted());
    expect(classSet(part("n-icon-products"))).toEqual(ICON_CLASSES.toSorted());
    expect(classSet(part("n-link-inquiries"))).toEqual(LINK_CLASSES.toSorted());
  });

  it("renders the portalled half with its recipes", async () => {
    await render(<SiteNav defaultValue="products" />);

    expect(classSet(part("n-backdrop"))).toEqual(BACKDROP_CLASSES.toSorted());
    expect(classSet(part("n-positioner"))).toEqual(POSITIONER_CLASSES.toSorted());
    expect(classSet(part("n-popup"))).toEqual(POPUP_CLASSES.toSorted());
    expect(classSet(part("n-arrow"))).toEqual(ARROW_CLASSES.toSorted());
    expect(classSet(part("n-viewport"))).toEqual(VIEWPORT_CLASSES.toSorted());
    expect(classSet(part("n-content-products"))).toEqual(CONTENT_CLASSES.toSorted());
    // An ACTIVE link keeps the same recipe: the current-page treatment is a
    // data-attribute modifier inside it, not a second class list.
    expect(classSet(part("n-link-settings"))).toEqual(LINK_CLASSES.toSorted());
  });

  it("keeps the panel mounted and hidden when keepMounted is asked for", async () => {
    // The crawler/SSR escape hatch. Measured: the Content stays in the viewport
    // carrying `data-closed` and the `hidden` attribute rather than unmounting,
    // so its recipe must still be on it.
    await render(
      <NavigationMenu.Root data-testid="k-root">
        <NavigationMenu.List data-testid="k-list">
          <NavigationMenu.Item data-testid="k-item">
            <NavigationMenu.Trigger data-testid="k-trigger">Products</NavigationMenu.Trigger>
            <NavigationMenu.Content data-testid="k-content" keepMounted>
              <NavigationMenu.Link data-testid="k-link" href="#apps">
                Apps
              </NavigationMenu.Link>
            </NavigationMenu.Content>
          </NavigationMenu.Item>
        </NavigationMenu.List>
        <NavigationMenu.Portal keepMounted>
          <NavigationMenu.Positioner data-testid="k-positioner">
            <NavigationMenu.Popup data-testid="k-popup">
              <NavigationMenu.Viewport data-testid="k-viewport" />
            </NavigationMenu.Popup>
          </NavigationMenu.Positioner>
        </NavigationMenu.Portal>
      </NavigationMenu.Root>,
    );

    const content = part("k-content");
    expect(content.hasAttribute("data-closed")).toBe(true);
    expect(content.hasAttribute("hidden")).toBe(true);
    expect(part("k-viewport").contains(content)).toBe(true);
    expect(part("k-popup").hasAttribute("data-closed")).toBe(true);
    expect(classSet(content)).toEqual(CONTENT_CLASSES.toSorted());
  });

  /*
   * ---------------------------------------------------------------------------
   * CALLER CONTRACTS: className override, the `render` prop, prop passthrough.
   * ---------------------------------------------------------------------------
   */

  it("lets a caller className override popup, trigger and link recipe utilities", async () => {
    // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
    // rather than appended-after, and untouched recipe utilities survive. A
    // string-append implementation leaves both background classes on and fails.
    await render(
      <NavigationMenu.Root data-testid="w-root" defaultValue="one" className="flex-col">
        <NavigationMenu.List data-testid="w-list" className="gap-4">
          <NavigationMenu.Item value="one">
            <NavigationMenu.Trigger data-testid="w-trigger" className="px-6 text-destructive">
              Products
            </NavigationMenu.Trigger>
            <NavigationMenu.Content data-testid="w-content" className="p-6">
              <NavigationMenu.Link data-testid="w-link" href="#apps" className="text-destructive">
                Apps
              </NavigationMenu.Link>
            </NavigationMenu.Content>
          </NavigationMenu.Item>
        </NavigationMenu.List>
        <NavigationMenu.Portal>
          <NavigationMenu.Positioner>
            <NavigationMenu.Popup data-testid="w-popup" className="bg-accent shadow-lg">
              <NavigationMenu.Viewport />
            </NavigationMenu.Popup>
          </NavigationMenu.Positioner>
        </NavigationMenu.Portal>
      </NavigationMenu.Root>,
    );

    const popup = part("w-popup");
    expect(popup.classList.contains("bg-accent")).toBe(true);
    expect(popup.classList.contains("bg-popover")).toBe(false);
    expect(popup.classList.contains("shadow-lg")).toBe(true);
    expect(popup.classList.contains("shadow-md")).toBe(false);
    expect(popup.classList.contains("border-border")).toBe(true);
    expect(popup.classList.contains("data-[ending-style]:scale-95")).toBe(true);

    const trigger = part("w-trigger");
    expect(trigger.classList.contains("px-6")).toBe(true);
    expect(trigger.classList.contains("px-3")).toBe(false);
    expect(trigger.classList.contains("text-destructive")).toBe(true);
    expect(trigger.classList.contains("data-[popup-open]:bg-accent")).toBe(true);

    const link = part("w-link");
    expect(link.classList.contains("text-destructive")).toBe(true);
    expect(link.classList.contains("text-foreground")).toBe(false);
    expect(link.classList.contains("no-underline")).toBe(true);

    // The rail direction and spacing a sidebar consumer flips, proving the
    // layout-neutral recipes do not fight it.
    expect(part("w-root").classList.contains("flex-col")).toBe(true);
    expect(part("w-list").classList.contains("gap-4")).toBe(true);
    expect(part("w-list").classList.contains("gap-1")).toBe(false);
    expect(part("w-content").classList.contains("p-6")).toBe(true);
    expect(part("w-content").classList.contains("p-2")).toBe(false);
  });

  it("carries the recipes onto other elements through the render prop", async () => {
    // Base UI's `render` prop is much of the reason this catalog moved onto Base
    // UI at all: the recipe has to travel to whatever element the caller
    // substitutes. For this component it is the load-bearing case — the Phase-4
    // sidebar renders every Link as a TanStack Router `Link`.
    await render(
      <NavigationMenu.Root data-testid="r-root" defaultValue="one" render={<aside />}>
        <NavigationMenu.List data-testid="r-list">
          <NavigationMenu.Item value="one">
            <NavigationMenu.Trigger data-testid="r-trigger">Products</NavigationMenu.Trigger>
            <NavigationMenu.Content>
              <NavigationMenu.Link data-testid="r-link" render={<button type="button" />}>
                Apps
              </NavigationMenu.Link>
            </NavigationMenu.Content>
          </NavigationMenu.Item>
        </NavigationMenu.List>
        <NavigationMenu.Portal>
          <NavigationMenu.Positioner>
            <NavigationMenu.Popup data-testid="r-popup" render={<div />}>
              <NavigationMenu.Viewport />
            </NavigationMenu.Popup>
          </NavigationMenu.Positioner>
        </NavigationMenu.Portal>
      </NavigationMenu.Root>,
    );

    expect(part("r-root").tagName).toBe("ASIDE");
    expect(classSet(part("r-root"))).toEqual(ROOT_CLASSES.toSorted());

    const popup = part("r-popup");
    expect(popup.tagName).toBe("DIV");
    expect(classSet(popup)).toEqual(POPUP_CLASSES.toSorted());

    const link = part("r-link");
    expect(link.tagName).toBe("BUTTON");
    expect(classSet(link)).toEqual(LINK_CLASSES.toSorted());
  });

  it("passes through app-owned data-testid and native attributes", async () => {
    await render(
      <NavigationMenu.Root defaultValue="one" aria-label="Dashboard">
        <NavigationMenu.List data-testid="dashboard-nav">
          <NavigationMenu.Item value="one">
            <NavigationMenu.Trigger data-testid="dashboard-nav-products" disabled>
              Products
            </NavigationMenu.Trigger>
            <NavigationMenu.Content>
              <NavigationMenu.Link
                data-testid="dashboard-nav-apps"
                href="#apps"
                aria-label="Applications"
              >
                Apps
              </NavigationMenu.Link>
            </NavigationMenu.Content>
          </NavigationMenu.Item>
        </NavigationMenu.List>
        <NavigationMenu.Portal>
          <NavigationMenu.Positioner>
            <NavigationMenu.Popup>
              <NavigationMenu.Viewport />
            </NavigationMenu.Popup>
          </NavigationMenu.Positioner>
        </NavigationMenu.Portal>
      </NavigationMenu.Root>,
    );

    // Measured: a disabled trigger is marked with `aria-disabled` and stays
    // focusable (`tabindex="0"`) rather than taking the native `disabled`
    // attribute — which is why its recipe uses an `aria-disabled:` modifier.
    const trigger = part("dashboard-nav-products");
    expect(trigger.getAttribute("aria-disabled")).toBe("true");
    expect(trigger.hasAttribute("disabled")).toBe(false);
    expect(trigger.getAttribute("tabindex")).toBe("0");
    expect(part("dashboard-nav-apps").getAttribute("aria-label")).toBe("Applications");
  });

  it("honours a controlled value and swaps the panel when it changes", async () => {
    const { rerender } = await render(<SiteNav value={null} />);

    expect(maybePart("n-popup")).toBeNull();

    await rerender(<SiteNav value="products" />);

    expect(part("n-content-products").hasAttribute("data-open")).toBe(true);
    expect(maybePart("n-content-company")).toBeNull();

    await rerender(<SiteNav value="company" />);

    // The SAME popup stays open; only the panel inside the viewport changes.
    await expect.poll(() => maybePart("n-content-products")).toBeNull();
    expect(part("n-content-company").hasAttribute("data-open")).toBe(true);
    expect(part("n-trigger-company").hasAttribute("data-popup-open")).toBe(true);
    expect(part("n-trigger-products").hasAttribute("data-popup-open")).toBe(false);
  });

  /*
   * ---------------------------------------------------------------------------
   * KEYBOARD. Nothing below touches the pointer, so these still run before the
   * pointer-driven specs (consequence 8). Focus is polled at every step because
   * Base UI moves it a tick after the key.
   * ---------------------------------------------------------------------------
   */

  it("roves focus along the triggers with the horizontal arrow keys", async () => {
    await render(<SiteNav />);

    part("n-trigger-products").focus();
    await expect.poll(focusedTestId).toBe("n-trigger-products");

    await userEvent.keyboard("{ArrowRight}");
    await expect.poll(focusedTestId).toBe("n-trigger-company");

    await userEvent.keyboard("{ArrowLeft}");
    await expect.poll(focusedTestId).toBe("n-trigger-products");
    // Roving alone opens nothing.
    expect(maybePart("n-popup")).toBeNull();
  });

  it("opens the panel with ArrowDown and lands focus on its first link", async () => {
    // The keyboard entry path a navigation menu is judged on: no pointer at all,
    // and the proof that the panel is reachable without a mouse.
    await render(<SiteNav />);

    part("n-trigger-products").focus();
    await userEvent.keyboard("{ArrowDown}");

    await expect.poll(focusedTestId).toBe("n-link-apps");
    expect(part("n-popup").hasAttribute("data-open")).toBe(true);
    expect(part("n-trigger-products").hasAttribute("data-popup-open")).toBe(true);
  });

  it("swaps the arrow-key axis when the orientation is vertical", async () => {
    // Consequence 9: nothing in the markup says "vertical", so the axis swap is
    // the only way to pin the orientation prop — and it is exactly what a
    // sidebar rail needs (up/down along the rail, right to enter the panel).
    await render(<SiteNav orientation="vertical" />);

    part("n-trigger-products").focus();

    await userEvent.keyboard("{ArrowDown}");
    await expect.poll(focusedTestId).toBe("n-trigger-company");
    expect(maybePart("n-popup")).toBeNull();

    await userEvent.keyboard("{ArrowUp}");
    await expect.poll(focusedTestId).toBe("n-trigger-products");

    await userEvent.keyboard("{ArrowRight}");
    await expect.poll(focusedTestId).toBe("n-link-apps");
    expect(part("n-popup").hasAttribute("data-open")).toBe(true);
  });

  it("closes and unmounts the panel on Escape and returns focus to the trigger", async () => {
    await render(<SiteNav />);

    part("n-trigger-products").focus();
    await userEvent.keyboard("{ArrowDown}");
    await expect.poll(focusedTestId).toBe("n-link-apps");

    await userEvent.keyboard("{Escape}");

    // Polled, not read once: some close paths defer the unmount by a frame.
    await expect.poll(() => maybePart("n-popup")).toBeNull();
    expect(maybePart("n-portal")).toBeNull();
    expect(maybePart("n-content-products")).toBeNull();
    expect(part("n-root").hasAttribute("data-open")).toBe(false);
    expect(part("n-trigger-products").getAttribute("aria-expanded")).toBe("false");
    expect(document.activeElement).toBe(part("n-trigger-products"));
  });

  /*
   * ---------------------------------------------------------------------------
   * POINTER-DRIVEN SPECS — LAST IN THE FILE (consequence 8: Playwright's mouse
   * position persists across specs, and this menu opens on hover).
   * ---------------------------------------------------------------------------
   */

  it("opens the panel when the trigger is clicked", async () => {
    await render(<SiteNav />);

    await userEvent.click(part("n-trigger-products"));

    await expect.poll(() => maybePart("n-popup")).not.toBeNull();
    expect(part("n-popup").hasAttribute("data-open")).toBe(true);
    expect(part("n-content-products").hasAttribute("data-open")).toBe(true);
    // Measured: a click leaves focus on the trigger — it does NOT enter the
    // panel the way ArrowDown does.
    expect(document.activeElement).toBe(part("n-trigger-products"));
  });

  it("reports the open item to onValueChange", async () => {
    // The caller's handler has to survive Base UI's own mergeProps.
    const onValueChange = vi.fn();
    await render(<SiteNav onValueChange={onValueChange} />);

    await userEvent.click(part("n-trigger-products"));

    await expect.poll(() => onValueChange.mock.calls.length).toBeGreaterThan(0);
    expect(onValueChange.mock.calls[0]?.[0]).toBe("products");
  });

  it("swaps the panel when a second trigger is pressed", async () => {
    // A DIRECT DOM click, not `userEvent.click`: the open popup lies on top of
    // the trigger list in this unstyled project, so Playwright's actionability
    // check on the second trigger never resolves. See consequence 2.
    await render(<SiteNav />);

    await userEvent.click(part("n-trigger-products"));
    await expect.poll(() => maybePart("n-content-products")).not.toBeNull();

    part("n-trigger-company").click();

    await expect.poll(() => maybePart("n-content-company")).not.toBeNull();
    // The outgoing panel is NOT gone at that moment: Base UI cross-fades the two
    // inside the shared viewport, so the old one lingers a frame carrying
    // `data-closed data-ending-style inert`. Polled, not read once.
    await expect.poll(() => maybePart("n-content-products")).toBeNull();
    expect(part("n-popup").hasAttribute("data-open")).toBe(true);
    expect(part("n-trigger-company").hasAttribute("data-popup-open")).toBe(true);
    expect(part("n-trigger-products").hasAttribute("data-popup-open")).toBe(false);
  });

  it("closes the panel when its own trigger is pressed again", async () => {
    await render(<SiteNav />);

    await userEvent.click(part("n-trigger-products"));
    await expect.poll(() => maybePart("n-popup")).not.toBeNull();

    part("n-trigger-products").click();

    await expect.poll(() => maybePart("n-popup")).toBeNull();
    expect(part("n-trigger-products").getAttribute("aria-expanded")).toBe("false");
  });

  it("closes the panel on an outside press", async () => {
    // Unlike Dialog and Preview Card, outside-press IS drivable from this
    // project: there is no modal blocker to swallow the press, and Base UI
    // listens on pointerdown, which a dispatched event reproduces faithfully.
    await render(
      <div>
        <span data-testid="n-outside">Elsewhere on the page</span>
        <SiteNav />
      </div>,
    );

    await userEvent.click(part("n-trigger-products"));
    await expect.poll(() => maybePart("n-popup")).not.toBeNull();

    const outside = part("n-outside");
    outside.dispatchEvent(new PointerEvent("pointerdown", { bubbles: true }));
    outside.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }));
    outside.dispatchEvent(new MouseEvent("click", { bubbles: true }));

    // Measured: this path is the animation-frame-deferred one — the popup is
    // still in the DOM synchronously here.
    await expect.poll(() => maybePart("n-popup")).toBeNull();
    expect(part("n-trigger-products").getAttribute("aria-expanded")).toBe("false");
  });

  it("opens the panel on hover, after the root's delay", async () => {
    // LAST SPEC IN THE FILE, per the standing "Playwright's mouse position
    // persists across specs" rule — this is the one that parks the pointer over
    // a trigger. Hover-open is not opt-in: there is no `openOnHover` prop, so a
    // consumer has to know it happens.
    await render(<SiteNav />);

    expect(maybePart("n-popup")).toBeNull();

    await userEvent.hover(part("n-trigger-products"));

    await expect.poll(() => maybePart("n-popup")).not.toBeNull();
    expect(part("n-content-products").hasAttribute("data-open")).toBe(true);
    expect(part("n-trigger-products").hasAttribute("data-popup-open")).toBe(true);
  });
});
