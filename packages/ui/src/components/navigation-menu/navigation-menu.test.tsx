import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { NavigationMenu } from "./navigation-menu";

/*
 * Browser coverage for NavigationMenu composition, props, and keyboard behavior.
 * Popup parts are portalled onto body and unmount after an animation frame.
 * This project loads no Tailwind; pointer interactions with overlapping panels
 * use direct DOM clicks. Links use hashes to keep the test iframe on this page.
 */
const ROOT_CLASSES = ["flex", "min-w-0"];

const LIST_CLASSES = ["m-0", "flex", "min-w-0", "list-none", "gap-1", "p-0"];

const ITEM_CLASSES = ["min-w-0", "list-none"];

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
  "text-foreground",
  "outline-none",
  "transition-colors",
  "hover:bg-accent",
  "hover:text-accent-foreground",
  "data-[popup-open]:bg-accent",
  "data-[popup-open]:text-accent-foreground",
  "aria-disabled:opacity-50",
];

const TRIGGER_SIDEBAR_CLASSES = [
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
  "text-sidebar-foreground",
  "outline-none",
  "transition-colors",
  "hover:bg-sidebar-accent",
  "hover:text-sidebar-foreground",
  "data-[popup-open]:bg-sidebar-accent",
  "data-[popup-open]:text-sidebar-foreground",
  "aria-disabled:opacity-50",
];

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

const CONTENT_CLASSES = ["flex", "min-w-0", "flex-col", "gap-1", "p-2"];

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

const BACKDROP_CLASSES = ["fixed", "inset-0"];

const POSITIONER_CLASSES = ["z-50", "outline-none"];

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

const VIEWPORT_CLASSES = ["relative", "overflow-hidden"];

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

function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function part(testId: string): HTMLElement {
  const element = document.body.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
  expect(element, `no element with data-testid="${testId}"`).not.toBeNull();
  return element as HTMLElement;
}

function maybePart(testId: string): HTMLElement | null {
  return document.body.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
}

function focusedTestId(): string | null {
  return document.activeElement?.getAttribute("data-testid") ?? null;
}

interface SiteNavProps {
  readonly defaultValue?: string;
  readonly value?: string | null;
  readonly orientation?: "horizontal" | "vertical";
  readonly onValueChange?: (value: string | null) => void;
}

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

  it("respells the trigger in the rail's palette, without leaking `surface` into the markup", async () => {
    await render(
      <NavigationMenu.Root data-testid="s-root" defaultValue="one">
        <NavigationMenu.List data-testid="s-list">
          <NavigationMenu.Item value="one">
            <NavigationMenu.Trigger data-testid="s-trigger" surface="sidebar">
              Products
            </NavigationMenu.Trigger>
            <NavigationMenu.Content data-testid="s-content">
              <NavigationMenu.Link data-testid="s-link" href="#apps" surface="sidebar">
                Apps
              </NavigationMenu.Link>
            </NavigationMenu.Content>
          </NavigationMenu.Item>
        </NavigationMenu.List>
        <NavigationMenu.Portal>
          <NavigationMenu.Positioner data-testid="s-positioner">
            <NavigationMenu.Popup data-testid="s-popup">
              <NavigationMenu.Viewport data-testid="s-viewport" />
            </NavigationMenu.Popup>
          </NavigationMenu.Positioner>
        </NavigationMenu.Portal>
      </NavigationMenu.Root>,
    );

    const trigger = part("s-trigger");
    expect(classSet(trigger)).toEqual(TRIGGER_SIDEBAR_CLASSES.toSorted());

    // `surface` is a RECIPE AXIS, not an attribute. A forgotten destructure
    // spreads it into the DOM as `surface="sidebar"` — invalid markup React
    // warns about, and the kind of thing a class-list assertion sails past
    // because the classes are still right.
    expect(trigger.hasAttribute("surface")).toBe(false);
    expect(part("s-link").hasAttribute("surface")).toBe(false);
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
    // The trigger's own rest text is out-merged like the link's below it — the
    // colour a `surface` arm now owns is still a colour a caller may take.
    expect(trigger.classList.contains("text-foreground")).toBe(false);
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

  it.each([false, true])(
    "opens with ArrowDown, keeps trigger focus, and enters links with Tab, initially hovered: %s",
    async (initiallyHovered) => {
      await render(<SiteNav />);

      if (initiallyHovered) {
        await userEvent.hover(part("n-trigger-products"));
        await expect.poll(() => maybePart("n-popup")).not.toBeNull();
      }

      // Unhover starts a delayed close if the pointer has already opened the menu.
      // Wait for that close before testing keyboard entry into a closed panel.
      await userEvent.unhover(part("n-trigger-products"));
      await expect.poll(() => maybePart("n-popup")).toBeNull();
      part("n-trigger-products").focus();
      await userEvent.keyboard("{ArrowDown}");

      await expect
        .poll(() => ({
          focused: focusedTestId(),
          popupOpen: maybePart("n-popup")?.hasAttribute("data-open") ?? false,
          triggerOpen: part("n-trigger-products").hasAttribute("data-popup-open"),
        }))
        .toEqual({ focused: "n-trigger-products", popupOpen: true, triggerOpen: true });

      await userEvent.tab();
      await expect.poll(focusedTestId).toBe("n-link-apps");
    },
  );

  it("swaps the arrow-key axis when the orientation is vertical", async () => {
    await render(<SiteNav orientation="vertical" />);

    await userEvent.unhover(part("n-trigger-products"));
    await expect.poll(() => maybePart("n-popup")).toBeNull();
    part("n-trigger-products").focus();

    await userEvent.keyboard("{ArrowDown}");
    await expect.poll(focusedTestId).toBe("n-trigger-company");
    expect(maybePart("n-popup")).toBeNull();

    await userEvent.keyboard("{ArrowUp}");
    await expect.poll(focusedTestId).toBe("n-trigger-products");

    await userEvent.keyboard("{ArrowRight}");
    await expect.poll(() => maybePart("n-popup")).not.toBeNull();
    expect(focusedTestId()).toBe("n-trigger-products");
    await userEvent.tab();
    await expect.poll(focusedTestId).toBe("n-link-apps");
    expect(part("n-popup").hasAttribute("data-open")).toBe(true);
  });

  it("closes and unmounts the panel on Escape and returns focus to the trigger", async () => {
    await render(<SiteNav />);

    await userEvent.unhover(part("n-trigger-products"));
    await expect.poll(() => maybePart("n-popup")).toBeNull();
    part("n-trigger-products").focus();
    await userEvent.keyboard("{ArrowDown}");
    await expect.poll(() => maybePart("n-popup")).not.toBeNull();
    expect(focusedTestId()).toBe("n-trigger-products");
    await userEvent.tab();
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

  it("opens the panel when the trigger is clicked", async () => {
    await render(<SiteNav />);

    await userEvent.click(part("n-trigger-products"));

    await expect.poll(() => maybePart("n-popup")).not.toBeNull();
    expect(part("n-popup").hasAttribute("data-open")).toBe(true);
    expect(part("n-content-products").hasAttribute("data-open")).toBe(true);
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

    await expect.poll(() => maybePart("n-popup")).toBeNull();
    expect(part("n-trigger-products").getAttribute("aria-expanded")).toBe("false");
  });

  it("opens the panel on hover, after the root's delay", async () => {
    await render(<SiteNav />);

    expect(maybePart("n-popup")).toBeNull();

    await userEvent.hover(part("n-trigger-products"));

    await expect.poll(() => maybePart("n-popup")).not.toBeNull();
    expect(part("n-content-products").hasAttribute("data-open")).toBe(true);
    expect(part("n-trigger-products").hasAttribute("data-popup-open")).toBe(true);
  });
});
