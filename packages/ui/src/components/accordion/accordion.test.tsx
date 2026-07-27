import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Accordion } from "./accordion";

/*
 * Accordion behavioural spec (Wallow-m5aq.4.1). Shaped after the Wallow-m5aq.2.1
 * Button and Wallow-m5aq.3.1 Dialog exemplars:
 *
 *   1. Runs in the vitest BROWSER project — real headless Chromium, real Base UI,
 *      real DOM. Nothing is mocked.
 *   2. Recipes are asserted THROUGH the component, never by importing
 *      `accordionPanelRecipe` and inspecting its return value: a recipe unit
 *      test would pass while the component forgot to apply it.
 *   3. Class assertions are an ORDER-FREE SET (`classSet`), because
 *      `cn()`/tailwind-merge is free to reorder. The `*_CLASSES` constants below
 *      are the single source of truth for what each recipe must contain — the
 *      green phase transcribes them into accordion.styles.ts.
 *   4. Stories carry the visual coverage (see accordion.stories.tsx); this file
 *      is only for the edges a screenshot cannot make.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <div data-orientation="vertical" dir="ltr">                <- Accordion.Root
 *     <div data-index="0" data-closed data-hidden>             <- Accordion.Item
 *       <h3 data-index="0" data-closed data-hidden>            <- Accordion.Header
 *         <button type="button" id aria-expanded="false"       <- Accordion.Trigger
 *                 aria-disabled="false" data-hidden tabindex="0">
 *       …and, only while the item is open:
 *       <div role="region" id aria-labelledby="<trigger id>"   <- Accordion.Panel
 *            data-index="0" data-open
 *            style="--accordion-panel-height: auto; --accordion-panel-width: auto">
 *
 * Five consequences worth knowing before editing this file:
 *
 *   - THE PANEL IS ABSENT, NOT HIDDEN, WHILE CLOSED. `keepMounted` defaults to
 *     false, so a closed panel is not in the DOM at all. `keepMounted` swaps that
 *     for a bare `hidden` attribute, and `hiddenUntilFound` (which overrides
 *     `keepMounted`) for `hidden="until-found"`. All three mechanisms are pinned
 *     below, because a spec that only checked "not visible" would pass against
 *     any of them and prove nothing.
 *   - THE OPEN STATE LIVES ON DIFFERENT ATTRIBUTES PER PART. Item, Header and
 *     Panel carry `data-open`/`data-closed`; the TRIGGER carries neither — it
 *     gets `data-panel-open` (plus `aria-expanded` and an `aria-controls`
 *     pointing at the panel id). A recipe that styled `data-[open]:` on the
 *     trigger would silently never fire, so the trigger's state assertions here
 *     are explicitly about `data-panel-open`.
 *   - `data-hidden` rides along on Item/Header/Trigger while the panel is hidden
 *     and disappears when it opens — measured, and the attribute a caller would
 *     otherwise reach for `data-closed` to express on the trigger.
 *   - CLOSING MAY BE ANIMATION-FRAME-DEFERRED. The panel is height-animated, so
 *     Base UI gates its unmount behind the transition. Measured in THIS project
 *     the unmount is synchronous (the browser project loads no Tailwind, so the
 *     recipe's `transition-[height]` never runs), but every absence assertion
 *     still goes through `await expect.poll(...)` rather than a bare synchronous
 *     `expect(...).toBeNull()` — the catalog-wide rule, and the only shape that
 *     survives the day someone gives this project a stylesheet.
 *   - `data-starting-style` / `data-ending-style` only exist DURING a transition,
 *     so no spec here asserts them on an element. They are pinned as
 *     `data-[starting-style]:` / `data-[ending-style]:` modifiers inside the
 *     recipe class sets instead, which is what the catalog actually owns.
 */

/** The list wrapper. Owns the frame; the items own the rules between them. */
const ROOT_CLASSES = ["w-full", "overflow-hidden", "rounded-md", "border", "border-border"];

/** One header/panel pair, separated from the next by a rule. */
const ITEM_CLASSES = ["border-b", "border-border", "last:border-b-0"];

/** The `h3`. A flex row so the trigger inside it can stretch to full width. */
const HEADER_CLASSES = ["flex"];

/**
 * The trigger. `data-[disabled]:` is the only state modifier: the open state is
 * `data-panel-open`, which the chevron a caller renders inside should rotate
 * off, not something this recipe needs to paint.
 */
const TRIGGER_CLASSES = [
  "flex",
  "w-full",
  "items-center",
  "justify-between",
  "gap-2",
  "px-4",
  "py-3",
  "text-left",
  "text-sm",
  "font-medium",
  "text-foreground",
  "outline-none",
  "transition-colors",
  "hover:bg-accent",
  "hover:text-accent-foreground",
  "data-[disabled]:opacity-50",
];

/**
 * The panel. This is the ONLY recipe in the pair that does real work: Base UI
 * publishes the measured height as the `--accordion-panel-height` custom
 * property (measured: present on every open panel), so the collapse is a plain
 * `height` transition from `0` in the starting/ending phases to that variable.
 * `overflow-hidden` is what makes the clipped content look collapsed on the way.
 *
 * Deliberately NO padding: the height variable is measured off this element, so
 * padding here would be animated too and the panel would never fully close. The
 * padding belongs on a wrapper inside the panel, which is also what Base UI's own
 * examples do.
 */
const PANEL_CLASSES = [
  "h-[var(--accordion-panel-height)]",
  "overflow-hidden",
  "text-sm",
  "text-muted-foreground",
  "transition-[height]",
  "duration-150",
  "data-[starting-style]:h-0",
  "data-[ending-style]:h-0",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function byTestId(container: HTMLElement, id: string): HTMLElement {
  const element = container.querySelector(`[data-testid="${id}"]`);
  expect(element, id).not.toBeNull();
  return element as HTMLElement;
}

function queryTestId(container: HTMLElement, id: string): HTMLElement | null {
  return container.querySelector(`[data-testid="${id}"]`);
}

/** A two-item accordion — the smallest shape that can show single-select. */
function TwoItems(props: Partial<Parameters<typeof Accordion.Root>[0]>): ReactElement {
  return (
    <Accordion.Root data-testid="root" {...props}>
      <Accordion.Item value="a" data-testid="item-a">
        <Accordion.Header data-testid="header-a">
          <Accordion.Trigger data-testid="trigger-a">Shipping</Accordion.Trigger>
        </Accordion.Header>
        <Accordion.Panel data-testid="panel-a">Ships in two days.</Accordion.Panel>
      </Accordion.Item>
      <Accordion.Item value="b" data-testid="item-b">
        <Accordion.Header data-testid="header-b">
          <Accordion.Trigger data-testid="trigger-b">Returns</Accordion.Trigger>
        </Accordion.Header>
        <Accordion.Panel data-testid="panel-b">Thirty days, no questions.</Accordion.Panel>
      </Accordion.Item>
    </Accordion.Root>
  );
}

describe("Accordion", () => {
  describe("anatomy", () => {
    it("exposes exactly Base UI's Accordion parts, under Base UI's names", async () => {
      // Pinned as an exact set: the catalog's rule is that a multi-part
      // component's namespace mirrors Base UI's part list 1:1, so both a
      // dropped part and an invented one are failures.
      expect(Object.keys(Accordion).toSorted()).toEqual([
        "Header",
        "Item",
        "Panel",
        "Root",
        "Trigger",
      ]);
    });

    it("renders each part as the element Base UI documents", async () => {
      const { container } = await render(<TwoItems defaultValue={["a"]} />);

      expect(byTestId(container, "root").tagName).toBe("DIV");
      expect(byTestId(container, "item-a").tagName).toBe("DIV");
      expect(byTestId(container, "header-a").tagName).toBe("H3");
      expect(byTestId(container, "trigger-a").tagName).toBe("BUTTON");
      expect(byTestId(container, "panel-a").tagName).toBe("DIV");
    });

    it("gives the trigger an explicit button type so it never submits a form", async () => {
      const { container } = await render(<TwoItems />);

      expect(byTestId(container, "trigger-a").getAttribute("type")).toBe("button");
    });

    it("renders each part's recipe", async () => {
      const { container } = await render(<TwoItems defaultValue={["a"]} />);

      expect(classSet(byTestId(container, "root"))).toEqual(ROOT_CLASSES.toSorted());
      expect(classSet(byTestId(container, "item-a"))).toEqual(ITEM_CLASSES.toSorted());
      expect(classSet(byTestId(container, "header-a"))).toEqual(HEADER_CLASSES.toSorted());
      expect(classSet(byTestId(container, "trigger-a"))).toEqual(TRIGGER_CLASSES.toSorted());
      expect(classSet(byTestId(container, "panel-a"))).toEqual(PANEL_CLASSES.toSorted());
    });

    it("composes a part onto another element through the render prop", async () => {
      const { container } = await render(
        <Accordion.Root data-testid="root" render={<section />}>
          <Accordion.Item value="a">
            <Accordion.Header>
              <Accordion.Trigger data-testid="trigger">Shipping</Accordion.Trigger>
            </Accordion.Header>
            <Accordion.Panel>Ships in two days.</Accordion.Panel>
          </Accordion.Item>
        </Accordion.Root>,
      );

      const root = byTestId(container, "root");
      expect(root.tagName).toBe("SECTION");
      expect(classSet(root)).toEqual(ROOT_CLASSES.toSorted());
    });
  });

  describe("className merging", () => {
    it("lets a caller className override a conflicting recipe utility", async () => {
      // The cn()/tailwind-merge proof: the conflicting recipe utility is REMOVED
      // rather than appended after, so "last one wins" holds for the caller.
      const { container } = await render(<TwoItems className="rounded-none" />);

      const root = byTestId(container, "root");
      expect(root.classList.contains("rounded-none")).toBe(true);
      expect(root.classList.contains("rounded-md")).toBe(false);
      expect(root.classList.contains("border-border")).toBe(true);
    });

    it("merges a caller className onto the trigger and the panel too", async () => {
      const { container } = await render(
        <Accordion.Root defaultValue={["a"]}>
          <Accordion.Item value="a">
            <Accordion.Header>
              <Accordion.Trigger className="py-6" data-testid="trigger">
                Shipping
              </Accordion.Trigger>
            </Accordion.Header>
            <Accordion.Panel className="text-foreground" data-testid="panel">
              Ships in two days.
            </Accordion.Panel>
          </Accordion.Item>
        </Accordion.Root>,
      );

      const trigger = byTestId(container, "trigger");
      expect(trigger.classList.contains("py-6")).toBe(true);
      expect(trigger.classList.contains("py-3")).toBe(false);
      expect(trigger.classList.contains("px-4")).toBe(true);

      const panel = byTestId(container, "panel");
      expect(panel.classList.contains("text-foreground")).toBe(true);
      expect(panel.classList.contains("text-muted-foreground")).toBe(false);
      expect(panel.classList.contains("overflow-hidden")).toBe(true);
    });
  });

  describe("opening and closing", () => {
    it("renders no panel at all while the item is closed", async () => {
      // Measured: `keepMounted` defaults to false, so this is an ABSENT element,
      // not a hidden one. Anything asserting "not visible" would pass against
      // three different mechanisms and prove nothing.
      const { container } = await render(<TwoItems />);

      expect(queryTestId(container, "panel-a")).toBeNull();
      expect(queryTestId(container, "panel-b")).toBeNull();
    });

    it("opens the panel when its trigger is pressed", async () => {
      const { container } = await render(<TwoItems />);

      await userEvent.click(byTestId(container, "trigger-a"));

      const panel = byTestId(container, "panel-a");
      expect(panel.textContent).toBe("Ships in two days.");
      expect(panel.getAttribute("data-open")).toBe("");
      expect(panel.hasAttribute("hidden")).toBe(false);
    });

    it("closes the panel again on a second press", async () => {
      const { container } = await render(<TwoItems />);
      const trigger = byTestId(container, "trigger-a");

      await userEvent.click(trigger);
      expect(queryTestId(container, "panel-a")).not.toBeNull();

      await userEvent.click(trigger);
      // Absence is polled, never asserted synchronously: the panel is
      // height-animated and Base UI gates the unmount behind the transition.
      await expect.poll(() => queryTestId(container, "panel-a")).toBeNull();
    });

    it("opens the panel named by defaultValue on first render", async () => {
      const { container } = await render(<TwoItems defaultValue={["a"]} />);

      expect(queryTestId(container, "panel-a")).not.toBeNull();
      expect(queryTestId(container, "panel-b")).toBeNull();
    });

    it("keeps only one panel open at a time by default", async () => {
      const { container } = await render(<TwoItems defaultValue={["a"]} />);

      await userEvent.click(byTestId(container, "trigger-b"));

      expect(queryTestId(container, "panel-b")).not.toBeNull();
      await expect.poll(() => queryTestId(container, "panel-a")).toBeNull();
    });

    it("keeps several panels open at once when multiple is set", async () => {
      const { container } = await render(<TwoItems multiple defaultValue={["a"]} />);

      await userEvent.click(byTestId(container, "trigger-b"));

      expect(queryTestId(container, "panel-a")).not.toBeNull();
      expect(queryTestId(container, "panel-b")).not.toBeNull();
    });

    it("hands the whole new value array to onValueChange", async () => {
      const onValueChange = vi.fn();
      const { container } = await render(<TwoItems onValueChange={onValueChange} />);

      await userEvent.click(byTestId(container, "trigger-b"));

      expect(onValueChange).toHaveBeenCalledTimes(1);
      expect(onValueChange.mock.calls[0]?.[0]).toEqual(["b"]);
    });

    it("obeys a controlled value and ignores its own press", async () => {
      const onValueChange = vi.fn();
      const { container } = await render(<TwoItems value={["a"]} onValueChange={onValueChange} />);

      await userEvent.click(byTestId(container, "trigger-a"));

      expect(onValueChange).toHaveBeenCalledTimes(1);
      // The parent owns the value, so the panel stays open until it says so.
      expect(queryTestId(container, "panel-a")).not.toBeNull();
    });
  });

  describe("keepMounted and hiddenUntilFound", () => {
    it("keeps the closed panel in the DOM behind a hidden attribute", async () => {
      const { container } = await render(
        <Accordion.Root>
          <Accordion.Item value="a">
            <Accordion.Header>
              <Accordion.Trigger data-testid="trigger">Shipping</Accordion.Trigger>
            </Accordion.Header>
            <Accordion.Panel keepMounted data-testid="panel">
              Ships in two days.
            </Accordion.Panel>
          </Accordion.Item>
        </Accordion.Root>,
      );

      const panel = byTestId(container, "panel");
      expect(panel.getAttribute("hidden")).toBe("");
      expect(panel.getAttribute("data-closed")).toBe("");
      expect(panel.getAttribute("data-hidden")).toBe("");

      await userEvent.click(byTestId(container, "trigger"));

      expect(panel.hasAttribute("hidden")).toBe(false);
      expect(panel.getAttribute("data-open")).toBe("");
    });

    it("uses hidden=until-found so find-in-page can reveal the panel", async () => {
      // `hiddenUntilFound` OVERRIDES `keepMounted` (both set here on purpose), so
      // the attribute value is the whole point: a bare `hidden` would be
      // invisible to the browser's own search.
      const { container } = await render(
        <Accordion.Root>
          <Accordion.Item value="a">
            <Accordion.Header>
              <Accordion.Trigger>Shipping</Accordion.Trigger>
            </Accordion.Header>
            <Accordion.Panel hiddenUntilFound keepMounted data-testid="panel">
              Ships in two days.
            </Accordion.Panel>
          </Accordion.Item>
        </Accordion.Root>,
      );

      expect(byTestId(container, "panel").getAttribute("hidden")).toBe("until-found");
    });
  });

  describe("accessibility wiring", () => {
    it("points the trigger at the panel and the panel back at the trigger", async () => {
      const { container } = await render(<TwoItems defaultValue={["a"]} />);

      const trigger = byTestId(container, "trigger-a");
      const panel = byTestId(container, "panel-a");

      expect(panel.getAttribute("role")).toBe("region");
      expect(panel.id).not.toBe("");
      expect(trigger.id).not.toBe("");
      expect(trigger.getAttribute("aria-controls")).toBe(panel.id);
      expect(panel.getAttribute("aria-labelledby")).toBe(trigger.id);
    });

    it("tracks the panel state on aria-expanded", async () => {
      const { container } = await render(<TwoItems />);
      const trigger = byTestId(container, "trigger-a");

      expect(trigger.getAttribute("aria-expanded")).toBe("false");

      await userEvent.click(trigger);
      expect(trigger.getAttribute("aria-expanded")).toBe("true");
    });

    it("publishes the open state as data-panel-open on the trigger, not data-open", async () => {
      // Measured, and the single easiest thing to get wrong here: a recipe that
      // styled `data-[open]:` on the trigger would never fire. Item, Header and
      // Panel do use data-open/data-closed; the trigger does not.
      const { container } = await render(<TwoItems />);
      const trigger = byTestId(container, "trigger-a");

      expect(trigger.hasAttribute("data-panel-open")).toBe(false);
      expect(trigger.hasAttribute("data-open")).toBe(false);

      await userEvent.click(trigger);

      expect(trigger.getAttribute("data-panel-open")).toBe("");
      expect(trigger.hasAttribute("data-open")).toBe(false);
      expect(byTestId(container, "item-a").getAttribute("data-open")).toBe("");
      expect(byTestId(container, "header-a").getAttribute("data-open")).toBe("");
    });

    it("marks the item, header and trigger closed and hidden while shut", async () => {
      const { container } = await render(<TwoItems />);

      const item = byTestId(container, "item-a");
      expect(item.getAttribute("data-closed")).toBe("");
      expect(item.getAttribute("data-hidden")).toBe("");
      expect(item.getAttribute("data-index")).toBe("0");

      expect(byTestId(container, "header-a").getAttribute("data-closed")).toBe("");
      expect(byTestId(container, "trigger-a").getAttribute("data-hidden")).toBe("");
      expect(byTestId(container, "item-b").getAttribute("data-index")).toBe("1");
    });

    it("stamps the orientation on the root", async () => {
      const { container } = await render(<TwoItems />);

      expect(byTestId(container, "root").getAttribute("data-orientation")).toBe("vertical");
    });

    it("moves focus to the trigger that was pressed", async () => {
      const { container } = await render(<TwoItems />);
      const trigger = byTestId(container, "trigger-b");

      await userEvent.click(trigger);

      expect(document.activeElement).toBe(trigger);
    });
  });

  describe("disabled", () => {
    it("publishes the disabled state to every part and refuses to open", async () => {
      const onValueChange = vi.fn();
      const { container } = await render(<TwoItems disabled onValueChange={onValueChange} />);

      expect(byTestId(container, "root").getAttribute("data-disabled")).toBe("");
      expect(byTestId(container, "item-a").getAttribute("data-disabled")).toBe("");
      expect(byTestId(container, "header-a").getAttribute("data-disabled")).toBe("");

      const trigger = byTestId(container, "trigger-a");
      expect(trigger.getAttribute("data-disabled")).toBe("");
      // Measured: Base UI marks the trigger aria-disabled rather than using the
      // native `disabled` attribute, so it stays focusable and announceable.
      expect(trigger.getAttribute("aria-disabled")).toBe("true");
      expect(trigger.hasAttribute("disabled")).toBe(false);

      trigger.click();

      expect(onValueChange).not.toHaveBeenCalled();
      expect(queryTestId(container, "panel-a")).toBeNull();
    });

    it("disables a single item without disabling its siblings", async () => {
      const { container } = await render(
        <Accordion.Root>
          <Accordion.Item value="a" disabled data-testid="item-a">
            <Accordion.Header>
              <Accordion.Trigger data-testid="trigger-a">Shipping</Accordion.Trigger>
            </Accordion.Header>
            <Accordion.Panel data-testid="panel-a">Ships in two days.</Accordion.Panel>
          </Accordion.Item>
          <Accordion.Item value="b" data-testid="item-b">
            <Accordion.Header>
              <Accordion.Trigger data-testid="trigger-b">Returns</Accordion.Trigger>
            </Accordion.Header>
            <Accordion.Panel data-testid="panel-b">Thirty days.</Accordion.Panel>
          </Accordion.Item>
        </Accordion.Root>,
      );

      expect(byTestId(container, "item-a").getAttribute("data-disabled")).toBe("");
      expect(byTestId(container, "item-b").hasAttribute("data-disabled")).toBe(false);

      await userEvent.click(byTestId(container, "trigger-b"));
      expect(queryTestId(container, "panel-b")).not.toBeNull();
    });
  });

  describe("keyboard", () => {
    it("opens the panel on Enter and closes it on Space", async () => {
      const { container } = await render(<TwoItems />);
      const trigger = byTestId(container, "trigger-a");

      trigger.focus();
      await userEvent.keyboard("{Enter}");
      expect(queryTestId(container, "panel-a")).not.toBeNull();

      await userEvent.keyboard(" ");
      await expect.poll(() => queryTestId(container, "panel-a")).toBeNull();
    });
  });
});
