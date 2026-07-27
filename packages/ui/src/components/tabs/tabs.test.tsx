import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Tabs } from "./tabs";

/*
 * Wallow-m5aq.4.2 — Tabs. Same spec shape as the Wave-1 exemplar
 * (Wallow-m5aq.2.1) and the Wave-2 exemplar (Wallow-m5aq.3.1): browser vitest
 * project, nothing mocked, the recipes asserted THROUGH the component, class
 * assertions as an order-free set.
 *
 * Nothing here is portalled, so unlike every Wave-2 overlay every query goes
 * through render()'s `container` and none of the popup gotchas apply.
 *
 * ANATOMY, measured against the installed Base UI 1.6.0 rather than read off the
 * docs (a throwaway probe spec, since deleted):
 *
 *   <div data-orientation data-activation-direction>                  <- Root
 *     <div role="tablist" data-orientation data-activation-direction>  <- List
 *       <button type="button" role="tab" aria-selected aria-disabled>  <- Tab
 *       <span role="presentation" style="--active-tab-left: …">        <- Indicator
 *     <div role="tabpanel" tabindex aria-labelledby>                   <- Panel
 *
 * Four measurements are worth stating, because each is easy to assume wrong:
 *   - the ACTIVE tab is marked `data-active` (plus `data-composite-item-active`
 *     and `tabindex="0"`), NOT `data-selected` — the recipe keys off
 *     `data-[active]:`, so a spec that pinned the wrong attribute would let a
 *     silently unpainted active tab through.
 *   - an inactive Panel is UNMOUNTED, not hidden. It only stays in the DOM under
 *     `keepMounted`, and then it carries `hidden` + `data-hidden` + `inert` +
 *     `tabindex="-1"`. That is why the panel recipe may never set a `display`
 *     utility unprefixed: it would beat the UA rule for the `hidden` attribute
 *     and paint a hidden panel. `data-[hidden]:hidden` restates the intent under
 *     a variant, where a caller's own `flex` cannot merge it away.
 *   - `activateOnFocus` defaults to FALSE, so an arrow key MOVES FOCUS ONLY and
 *     Enter/Space activates. A disabled tab is still reachable by arrow (the
 *     roving composite includes it) but Enter does not activate it.
 *   - `Tabs.Indicator` renders NO ELEMENT while no tab is active, and when it
 *     does render it carries six inline `--active-tab-*` custom properties. The
 *     indicator recipe consumes those vars, so its geometry is Base UI's and its
 *     paint is the catalog's.
 *
 * Base UI stamps NO class of its own on any of the five parts (probed), so every
 * class set below is pure recipe and is asserted with no spread-in extras.
 */

/** Every utility `Tabs.Root` must render. Single source of truth. */
const ROOT_CLASSES = [
  "flex",
  "flex-col",
  "gap-3",
  "data-[orientation=vertical]:flex-row",
  "data-[orientation=vertical]:gap-4",
];

/** Every utility `Tabs.List` must render. `relative` is what the Indicator hangs off. */
const LIST_CLASSES = [
  "relative",
  "flex",
  "items-center",
  "gap-1",
  "border-b",
  "border-border",
  "data-[orientation=vertical]:flex-col",
  "data-[orientation=vertical]:items-stretch",
  "data-[orientation=vertical]:border-b-0",
  "data-[orientation=vertical]:border-r",
];

/** Every utility `Tabs.Tab` must render. */
const TAB_CLASSES = [
  "inline-flex",
  "items-center",
  "justify-center",
  "gap-2",
  "whitespace-nowrap",
  "rounded-t-md",
  "px-3",
  "py-2",
  "text-sm",
  "font-medium",
  "text-muted-foreground",
  "transition-colors",
  "hover:text-foreground",
  "data-[active]:text-foreground",
  "data-[disabled]:opacity-50",
];

/**
 * Every utility `Tabs.Indicator` must render. The four `var(--active-tab-*)`
 * arbitrary values are the whole point of the part: Base UI measures the active
 * tab and writes those custom properties inline, and these utilities turn them
 * into a rule that slides.
 */
const INDICATOR_CLASSES = [
  "absolute",
  "bottom-0",
  "left-0",
  "h-0.5",
  "w-[var(--active-tab-width)]",
  "translate-x-[var(--active-tab-left)]",
  "rounded-full",
  "bg-primary",
  "transition-all",
  "duration-150",
  "data-[orientation=vertical]:top-0",
  "data-[orientation=vertical]:right-0",
  "data-[orientation=vertical]:bottom-auto",
  "data-[orientation=vertical]:left-auto",
  "data-[orientation=vertical]:h-[var(--active-tab-height)]",
  "data-[orientation=vertical]:w-0.5",
  "data-[orientation=vertical]:translate-x-0",
  "data-[orientation=vertical]:translate-y-[var(--active-tab-top)]",
];

/** Every utility `Tabs.Panel` must render — no unprefixed `display`, see above. */
const PANEL_CLASSES = ["text-sm", "text-foreground", "outline-none", "data-[hidden]:hidden"];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function part(container: HTMLElement, testId: string): HTMLElement {
  const element = container.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
  expect(element, testId).not.toBeNull();
  return element as HTMLElement;
}

function maybePart(container: HTMLElement, testId: string): HTMLElement | null {
  return container.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
}

interface FixtureProps {
  readonly value?: string | null;
  readonly defaultValue?: string | null;
  readonly onValueChange?: (value: string) => void;
  readonly orientation?: "horizontal" | "vertical";
  readonly activateOnFocus?: boolean;
  readonly keepMounted?: boolean;
  readonly tabClassName?: string;
}

/**
 * The three-tab fixture every case below starts from. The third tab is disabled
 * so the keyboard cases can prove Base UI's roving composite reaches it and
 * still refuses to activate it.
 */
function renderTabs({
  activateOnFocus,
  keepMounted,
  tabClassName,
  ...rootProps
}: FixtureProps = {}) {
  return render(
    <Tabs.Root data-testid="root" {...rootProps}>
      <Tabs.List data-testid="list" activateOnFocus={activateOnFocus}>
        <Tabs.Tab value="account" data-testid="tab-account" className={tabClassName}>
          Account
        </Tabs.Tab>
        <Tabs.Tab value="password" data-testid="tab-password">
          Password
        </Tabs.Tab>
        <Tabs.Tab value="billing" data-testid="tab-billing" disabled>
          Billing
        </Tabs.Tab>
        <Tabs.Indicator data-testid="indicator" />
      </Tabs.List>
      <Tabs.Panel value="account" data-testid="panel-account" keepMounted={keepMounted}>
        Account panel
      </Tabs.Panel>
      <Tabs.Panel value="password" data-testid="panel-password" keepMounted={keepMounted}>
        Password panel
      </Tabs.Panel>
    </Tabs.Root>,
  );
}

describe("Tabs", () => {
  describe("anatomy", () => {
    it("mirrors Base UI's five namespace members exactly", () => {
      expect(Object.keys(Tabs).toSorted()).toEqual(["Indicator", "List", "Panel", "Root", "Tab"]);
    });

    it("renders the root, tablist, tab buttons and the active panel", async () => {
      const { container } = await renderTabs({ defaultValue: "account" });

      expect(part(container, "root").tagName).toBe("DIV");
      expect(part(container, "root").getAttribute("data-orientation")).toBe("horizontal");
      expect(part(container, "list").getAttribute("role")).toBe("tablist");
      expect(part(container, "tab-account").tagName).toBe("BUTTON");
      expect(part(container, "tab-account").getAttribute("role")).toBe("tab");
      expect(part(container, "panel-account").getAttribute("role")).toBe("tabpanel");
    });

    it("marks the active tab data-active and names its panel", async () => {
      // `data-active`, NOT `data-selected` — the attribute the tab recipe keys
      // off, so pinning it here is what keeps the recipe honest.
      const { container } = await renderTabs({ defaultValue: "account" });

      const active = part(container, "tab-account");
      expect(active.hasAttribute("data-active")).toBe(true);
      expect(active.getAttribute("aria-selected")).toBe("true");
      expect(part(container, "tab-password").hasAttribute("data-active")).toBe(false);
      expect(part(container, "panel-account").getAttribute("aria-labelledby")).toBe(
        active.getAttribute("id"),
      );
    });

    it("unmounts the inactive panel rather than hiding it", async () => {
      const { container } = await renderTabs({ defaultValue: "account" });

      expect(maybePart(container, "panel-account")).not.toBeNull();
      expect(maybePart(container, "panel-password")).toBeNull();
    });

    it("keeps an inactive panel in the DOM under keepMounted, hidden and inert", async () => {
      const { container } = await renderTabs({ defaultValue: "account", keepMounted: true });

      const hiddenPanel = part(container, "panel-password");
      expect(hiddenPanel.hasAttribute("hidden")).toBe(true);
      expect(hiddenPanel.hasAttribute("data-hidden")).toBe(true);
      expect(hiddenPanel.hasAttribute("inert")).toBe(true);
      expect(hiddenPanel.getAttribute("tabindex")).toBe("-1");
      expect(part(container, "panel-account").hasAttribute("data-hidden")).toBe(false);
    });

    it("publishes the vertical orientation every recipe keys off", async () => {
      const { container } = await renderTabs({ defaultValue: "account", orientation: "vertical" });

      expect(part(container, "root").getAttribute("data-orientation")).toBe("vertical");
      expect(part(container, "list").getAttribute("data-orientation")).toBe("vertical");
      expect(part(container, "list").getAttribute("aria-orientation")).toBe("vertical");
      expect(part(container, "tab-account").getAttribute("data-orientation")).toBe("vertical");
    });
  });

  describe("recipes", () => {
    it("paints the root and the tab strip", async () => {
      const { container } = await renderTabs({ defaultValue: "account" });

      expect(classSet(part(container, "root"))).toEqual(ROOT_CLASSES.toSorted());
      expect(classSet(part(container, "list"))).toEqual(LIST_CLASSES.toSorted());
    });

    it("paints every tab the same, active or not", async () => {
      // One recipe for all three states: active/disabled are `data-[…]:`
      // modifiers inside it, so the class SET never varies by state.
      const { container } = await renderTabs({ defaultValue: "account" });

      for (const testId of ["tab-account", "tab-password", "tab-billing"]) {
        expect(classSet(part(container, testId)), testId).toEqual(TAB_CLASSES.toSorted());
      }
    });

    it("paints the indicator with the active-tab custom properties", async () => {
      const { container } = await renderTabs({ defaultValue: "account" });

      expect(classSet(part(container, "indicator"))).toEqual(INDICATOR_CLASSES.toSorted());
    });

    it("paints the panel without an unprefixed display utility", async () => {
      const { container } = await renderTabs({ defaultValue: "account" });

      expect(classSet(part(container, "panel-account"))).toEqual(PANEL_CLASSES.toSorted());
    });
  });

  describe("indicator", () => {
    it("carries the six geometry custom properties Base UI measures", async () => {
      const { container } = await renderTabs({ defaultValue: "account" });

      const style = part(container, "indicator").getAttribute("style") ?? "";
      for (const property of [
        "--active-tab-left",
        "--active-tab-right",
        "--active-tab-top",
        "--active-tab-bottom",
        "--active-tab-width",
        "--active-tab-height",
      ]) {
        expect(style, property).toContain(property);
      }
    });

    it("renders no element at all while no tab is active", async () => {
      // The recipe styles a part that may legitimately be absent — a caller who
      // starts a controlled Tabs at `null` must not get a stray rule painted.
      const { container } = await renderTabs({ value: null });

      expect(maybePart(container, "indicator")).toBeNull();
    });
  });

  describe("keyboard navigation", () => {
    it("moves focus with the arrow keys without activating, by default", async () => {
      // `activateOnFocus` defaults to false: this is a roving-tabindex composite,
      // so the arrow key moves the single tab stop and nothing else changes.
      const { container } = await renderTabs({ defaultValue: "account" });

      part(container, "tab-account").focus();
      await userEvent.keyboard("{ArrowRight}");

      expect(document.activeElement).toBe(part(container, "tab-password"));
      expect(part(container, "tab-password").getAttribute("tabindex")).toBe("0");
      expect(part(container, "tab-account").getAttribute("tabindex")).toBe("-1");
      expect(part(container, "tab-password").getAttribute("aria-selected")).toBe("false");
      expect(part(container, "tab-account").hasAttribute("data-active")).toBe(true);
      expect(maybePart(container, "panel-password")).toBeNull();
    });

    it("activates the focused tab on Enter and swaps the mounted panel", async () => {
      const onValueChange = vi.fn();
      const { container } = await renderTabs({ defaultValue: "account", onValueChange });

      part(container, "tab-account").focus();
      await userEvent.keyboard("{ArrowRight}");
      await userEvent.keyboard("{Enter}");

      expect(onValueChange).toHaveBeenCalledTimes(1);
      expect(onValueChange.mock.calls[0]?.[0]).toBe("password");
      expect(part(container, "tab-password").hasAttribute("data-active")).toBe(true);
      await expect.poll(() => maybePart(container, "panel-password")).not.toBeNull();
      await expect.poll(() => maybePart(container, "panel-account")).toBeNull();
      expect(part(container, "root").getAttribute("data-activation-direction")).toBe("right");
    });

    it("activates as focus moves when the list opts into activateOnFocus", async () => {
      const { container } = await renderTabs({ defaultValue: "account", activateOnFocus: true });

      part(container, "tab-account").focus();
      await userEvent.keyboard("{ArrowRight}");

      expect(part(container, "tab-password").getAttribute("aria-selected")).toBe("true");
      await expect.poll(() => maybePart(container, "panel-password")).not.toBeNull();
    });

    it("follows the vertical axis when the root is vertical", async () => {
      const { container } = await renderTabs({ defaultValue: "account", orientation: "vertical" });

      part(container, "tab-account").focus();
      await userEvent.keyboard("{ArrowDown}");

      expect(document.activeElement).toBe(part(container, "tab-password"));
    });

    it("reaches a disabled tab by arrow but refuses to activate it", async () => {
      const onValueChange = vi.fn();
      const { container } = await renderTabs({ defaultValue: "account", onValueChange });

      part(container, "tab-billing").focus();
      await userEvent.keyboard("{Enter}");

      expect(part(container, "tab-billing").hasAttribute("data-disabled")).toBe(true);
      expect(part(container, "tab-billing").getAttribute("aria-disabled")).toBe("true");
      expect(onValueChange).not.toHaveBeenCalled();
      expect(part(container, "tab-account").hasAttribute("data-active")).toBe(true);
    });
  });

  describe("selection", () => {
    it("activates a tab on click and reports the new value", async () => {
      const onValueChange = vi.fn();
      const { container } = await renderTabs({ defaultValue: "account", onValueChange });

      await userEvent.click(part(container, "tab-password"));

      expect(onValueChange).toHaveBeenCalledTimes(1);
      expect(onValueChange.mock.calls[0]?.[0]).toBe("password");
      await expect.poll(() => maybePart(container, "panel-password")).not.toBeNull();
      await expect.poll(() => maybePart(container, "panel-account")).toBeNull();
    });

    it("stays put when the root is controlled and the caller ignores the change", async () => {
      // A controlled `value` with no state update must not self-update — the
      // proof that `value` is genuinely controlled rather than an initial seed.
      const onValueChange = vi.fn();
      const { container } = await renderTabs({ value: "account", onValueChange });

      await userEvent.click(part(container, "tab-password"));

      expect(onValueChange.mock.calls[0]?.[0]).toBe("password");
      expect(part(container, "tab-account").hasAttribute("data-active")).toBe(true);
      expect(part(container, "tab-password").hasAttribute("data-active")).toBe(false);
      expect(maybePart(container, "panel-password")).toBeNull();
    });
  });

  describe("composition", () => {
    it("lets a caller className override a recipe utility on a tab", async () => {
      // The cn()/tailwind-merge proof for this component: the conflicting
      // font-size utility is REMOVED rather than appended after, while the
      // utilities the caller never mentioned survive.
      const { container } = await renderTabs({
        defaultValue: "account",
        tabClassName: "text-base",
      });

      const tab = part(container, "tab-account");
      expect(tab.classList.contains("text-base")).toBe(true);
      expect(tab.classList.contains("text-sm")).toBe(false);
      expect(tab.classList.contains("text-muted-foreground")).toBe(true);
      expect(tab.classList.contains("data-[active]:text-foreground")).toBe(true);
    });

    it("composes the root recipe onto another element through the render prop", async () => {
      const { container } = await render(
        <Tabs.Root defaultValue="account" render={<section data-testid="root" />}>
          <Tabs.List>
            <Tabs.Tab value="account">Account</Tabs.Tab>
          </Tabs.List>
        </Tabs.Root>,
      );

      const section = container.querySelector("section");
      expect(section).not.toBeNull();
      expect(classSet(section as Element)).toEqual(ROOT_CLASSES.toSorted());
    });

    it("passes an app-owned aria-label and data-testid through to the tablist", async () => {
      const { container } = await render(
        <Tabs.Root defaultValue="account">
          <Tabs.List data-testid="settings-tablist" aria-label="Settings sections">
            <Tabs.Tab value="account">Account</Tabs.Tab>
          </Tabs.List>
        </Tabs.Root>,
      );

      expect(part(container, "settings-tablist").getAttribute("aria-label")).toBe(
        "Settings sections",
      );
    });
  });
});
