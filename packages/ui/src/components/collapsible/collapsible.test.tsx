import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Collapsible } from "./collapsible";

/*
 * Collapsible behavioural spec (Wallow-m5aq.4.1, the standalone half of the
 * Accordion pair). Same rules as accordion.test.tsx: browser project, nothing
 * mocked, recipes asserted THROUGH the component, class assertions as an
 * order-free set, and the `*_CLASSES` constants below are the source of truth
 * the green phase transcribes into collapsible.styles.ts.
 *
 * ANATOMY, measured against @base-ui/react 1.6.0 in this browser (not guessed):
 *
 *   <div data-closed>                                        <- Collapsible.Root
 *     <button type="button" aria-expanded="false"            <- Collapsible.Trigger
 *             aria-disabled="false" tabindex="0">
 *     …and, only while open:
 *     <div id data-open                                      <- Collapsible.Panel
 *          style="--collapsible-panel-height: auto; --collapsible-panel-width: auto">
 *
 * Where this differs from `../accordion`, and why each difference is pinned:
 *
 *   - THE ROOT carries `data-open`/`data-closed` itself. `Accordion.Root` does
 *     not (it has a value LIST, so there is no single open state) — it stamps
 *     `data-orientation` instead, which this root does not have.
 *   - THE PANEL IS A PLAIN `<div>`: no `role="region"`, no `aria-labelledby`
 *     back at the trigger. Only the trigger's `aria-controls` links the two.
 *     Asserting a region role here would be copying the accordion's anatomy onto
 *     a component that does not have it.
 *   - The panel's custom property is `--collapsible-panel-height`, NOT
 *     `--accordion-panel-height`. The two recipes are otherwise the same shape,
 *     and swapping the variable is the easiest silent mistake to make when this
 *     file is written next to the accordion's.
 *
 * The rules shared with the accordion still hold: the panel is ABSENT rather
 * than hidden while closed (`keepMounted` -> bare `hidden`, `hiddenUntilFound` ->
 * `hidden="until-found"`), the trigger's open state is `data-panel-open` and
 * never `data-open`, absence assertions go through `expect.poll`, and
 * `data-starting-style`/`data-ending-style` are pinned as recipe modifiers
 * rather than asserted on an element.
 */

/** The wrapper. Deliberately layout-only: a collapsible is embedded in whatever
 * frame the caller already has, so a border here would fight it. */
const ROOT_CLASSES = ["w-full"];

/** The trigger. Same treatment as the accordion's, minus the full-width row —
 * a standalone collapsible's trigger sits inline next to other content. */
const TRIGGER_CLASSES = [
  "inline-flex",
  "items-center",
  "justify-between",
  "gap-2",
  "rounded-md",
  "px-3",
  "py-2",
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
 * The panel. Base UI publishes the measured height as
 * `--collapsible-panel-height`, so the collapse is a plain `height` transition
 * from `0` in the starting/ending phases to that variable, clipped by
 * `overflow-hidden`. No padding, for the same reason as the accordion's panel:
 * the height variable is measured off THIS element, so padding here would be
 * animated too and the panel would never fully close.
 */
const PANEL_CLASSES = [
  "h-[var(--collapsible-panel-height)]",
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

function Subject(props: Partial<Parameters<typeof Collapsible.Root>[0]>): ReactElement {
  return (
    <Collapsible.Root data-testid="root" {...props}>
      <Collapsible.Trigger data-testid="trigger">Advanced settings</Collapsible.Trigger>
      <Collapsible.Panel data-testid="panel">Retention policy and webhooks.</Collapsible.Panel>
    </Collapsible.Root>
  );
}

describe("Collapsible", () => {
  describe("anatomy", () => {
    it("exposes exactly Base UI's Collapsible parts, under Base UI's names", async () => {
      // Pinned as an exact set: a multi-part component's namespace mirrors Base
      // UI's part list 1:1, so both a dropped part and an invented one fail.
      expect(Object.keys(Collapsible).toSorted()).toEqual(["Panel", "Root", "Trigger"]);
    });

    it("renders each part as the element Base UI documents", async () => {
      const { container } = await render(<Subject defaultOpen />);

      expect(byTestId(container, "root").tagName).toBe("DIV");
      expect(byTestId(container, "trigger").tagName).toBe("BUTTON");
      expect(byTestId(container, "panel").tagName).toBe("DIV");
    });

    it("gives the trigger an explicit button type so it never submits a form", async () => {
      const { container } = await render(<Subject />);

      expect(byTestId(container, "trigger").getAttribute("type")).toBe("button");
    });

    it("renders each part's recipe", async () => {
      const { container } = await render(<Subject defaultOpen />);

      expect(classSet(byTestId(container, "root"))).toEqual(ROOT_CLASSES.toSorted());
      expect(classSet(byTestId(container, "trigger"))).toEqual(TRIGGER_CLASSES.toSorted());
      expect(classSet(byTestId(container, "panel"))).toEqual(PANEL_CLASSES.toSorted());
    });

    it("leaves the panel a plain div, with no region role of its own", async () => {
      // The anatomy difference from Accordion.Panel, pinned so nobody copies the
      // accordion's role/aria-labelledby pair onto a component without them.
      const { container } = await render(<Subject defaultOpen />);

      const panel = byTestId(container, "panel");
      expect(panel.hasAttribute("role")).toBe(false);
      expect(panel.hasAttribute("aria-labelledby")).toBe(false);
    });

    it("composes a part onto another element through the render prop", async () => {
      const { container } = await render(<Subject render={<section />} />);

      const root = byTestId(container, "root");
      expect(root.tagName).toBe("SECTION");
      expect(classSet(root)).toEqual(ROOT_CLASSES.toSorted());
    });
  });

  describe("className merging", () => {
    it("lets a caller className override a conflicting recipe utility", async () => {
      const { container } = await render(<Subject className="w-64" />);

      const root = byTestId(container, "root");
      expect(root.classList.contains("w-64")).toBe(true);
      expect(root.classList.contains("w-full")).toBe(false);
    });

    it("merges a caller className onto the trigger and the panel too", async () => {
      const { container } = await render(
        <Collapsible.Root defaultOpen>
          <Collapsible.Trigger className="px-6" data-testid="trigger">
            Advanced settings
          </Collapsible.Trigger>
          <Collapsible.Panel className="text-foreground" data-testid="panel">
            Retention policy.
          </Collapsible.Panel>
        </Collapsible.Root>,
      );

      const trigger = byTestId(container, "trigger");
      expect(trigger.classList.contains("px-6")).toBe(true);
      expect(trigger.classList.contains("px-3")).toBe(false);
      expect(trigger.classList.contains("py-2")).toBe(true);

      const panel = byTestId(container, "panel");
      expect(panel.classList.contains("text-foreground")).toBe(true);
      expect(panel.classList.contains("text-muted-foreground")).toBe(false);
      expect(panel.classList.contains("overflow-hidden")).toBe(true);
    });
  });

  describe("opening and closing", () => {
    it("renders no panel at all while closed", async () => {
      // Measured: `keepMounted` defaults to false, so this is an ABSENT element.
      const { container } = await render(<Subject />);

      expect(queryTestId(container, "panel")).toBeNull();
    });

    it("opens the panel when the trigger is pressed", async () => {
      const { container } = await render(<Subject />);

      await userEvent.click(byTestId(container, "trigger"));

      const panel = byTestId(container, "panel");
      expect(panel.textContent).toBe("Retention policy and webhooks.");
      expect(panel.getAttribute("data-open")).toBe("");
      expect(panel.hasAttribute("hidden")).toBe(false);
    });

    it("closes the panel again on a second press", async () => {
      const { container } = await render(<Subject />);
      const trigger = byTestId(container, "trigger");

      await userEvent.click(trigger);
      expect(queryTestId(container, "panel")).not.toBeNull();

      await userEvent.click(trigger);
      await expect.poll(() => queryTestId(container, "panel")).toBeNull();
    });

    it("opens on first render when defaultOpen is set", async () => {
      const { container } = await render(<Subject defaultOpen />);

      expect(queryTestId(container, "panel")).not.toBeNull();
      expect(byTestId(container, "root").getAttribute("data-open")).toBe("");
    });

    it("tells the caller the new open state through onOpenChange", async () => {
      const onOpenChange = vi.fn();
      const { container } = await render(<Subject onOpenChange={onOpenChange} />);

      await userEvent.click(byTestId(container, "trigger"));

      expect(onOpenChange).toHaveBeenCalledTimes(1);
      expect(onOpenChange.mock.calls[0]?.[0]).toBe(true);
    });

    it("obeys a controlled open and ignores its own press", async () => {
      const onOpenChange = vi.fn();
      const { container } = await render(<Subject open onOpenChange={onOpenChange} />);

      await userEvent.click(byTestId(container, "trigger"));

      expect(onOpenChange).toHaveBeenCalledTimes(1);
      expect(onOpenChange.mock.calls[0]?.[0]).toBe(false);
      // The parent owns `open`, so the panel stays put until it says otherwise.
      expect(queryTestId(container, "panel")).not.toBeNull();
    });

    it("publishes the open state on the root and swaps data-closed for data-open", async () => {
      const { container } = await render(<Subject />);
      const root = byTestId(container, "root");

      expect(root.getAttribute("data-closed")).toBe("");
      expect(root.hasAttribute("data-open")).toBe(false);

      await userEvent.click(byTestId(container, "trigger"));

      expect(root.getAttribute("data-open")).toBe("");
      expect(root.hasAttribute("data-closed")).toBe(false);
    });
  });

  describe("keepMounted and hiddenUntilFound", () => {
    it("keeps the closed panel in the DOM behind a hidden attribute", async () => {
      const { container } = await render(
        <Collapsible.Root>
          <Collapsible.Trigger data-testid="trigger">Advanced settings</Collapsible.Trigger>
          <Collapsible.Panel keepMounted data-testid="panel">
            Retention policy.
          </Collapsible.Panel>
        </Collapsible.Root>,
      );

      const panel = byTestId(container, "panel");
      expect(panel.getAttribute("hidden")).toBe("");
      expect(panel.getAttribute("data-closed")).toBe("");

      await userEvent.click(byTestId(container, "trigger"));

      expect(panel.hasAttribute("hidden")).toBe(false);
      expect(panel.getAttribute("data-open")).toBe("");
    });

    it("uses hidden=until-found so find-in-page can reveal the panel", async () => {
      // `hiddenUntilFound` OVERRIDES `keepMounted` (both set here on purpose).
      const { container } = await render(
        <Collapsible.Root>
          <Collapsible.Trigger>Advanced settings</Collapsible.Trigger>
          <Collapsible.Panel hiddenUntilFound keepMounted data-testid="panel">
            Retention policy.
          </Collapsible.Panel>
        </Collapsible.Root>,
      );

      expect(byTestId(container, "panel").getAttribute("hidden")).toBe("until-found");
    });
  });

  describe("accessibility wiring", () => {
    it("points the trigger at the panel once it is open", async () => {
      const { container } = await render(<Subject />);
      const trigger = byTestId(container, "trigger");

      expect(trigger.getAttribute("aria-expanded")).toBe("false");
      expect(trigger.hasAttribute("aria-controls")).toBe(false);

      await userEvent.click(trigger);

      const panel = byTestId(container, "panel");
      expect(panel.id).not.toBe("");
      expect(trigger.getAttribute("aria-expanded")).toBe("true");
      expect(trigger.getAttribute("aria-controls")).toBe(panel.id);
    });

    it("publishes the open state as data-panel-open on the trigger, not data-open", async () => {
      // Same trap as the accordion's trigger: a recipe styling `data-[open]:`
      // here would never fire.
      const { container } = await render(<Subject />);
      const trigger = byTestId(container, "trigger");

      expect(trigger.hasAttribute("data-panel-open")).toBe(false);

      await userEvent.click(trigger);

      expect(trigger.getAttribute("data-panel-open")).toBe("");
      expect(trigger.hasAttribute("data-open")).toBe(false);
    });
  });

  describe("disabled", () => {
    it("publishes the disabled state and refuses to open", async () => {
      const onOpenChange = vi.fn();
      const { container } = await render(<Subject disabled onOpenChange={onOpenChange} />);

      expect(byTestId(container, "root").getAttribute("data-disabled")).toBe("");

      const trigger = byTestId(container, "trigger");
      expect(trigger.getAttribute("data-disabled")).toBe("");
      // Measured: aria-disabled rather than the native `disabled` attribute, so
      // the trigger stays focusable and announceable.
      expect(trigger.getAttribute("aria-disabled")).toBe("true");
      expect(trigger.hasAttribute("disabled")).toBe(false);

      trigger.click();

      expect(onOpenChange).not.toHaveBeenCalled();
      expect(queryTestId(container, "panel")).toBeNull();
    });
  });

  describe("keyboard", () => {
    it("opens the panel on Enter and closes it on Space", async () => {
      const { container } = await render(<Subject />);
      const trigger = byTestId(container, "trigger");

      trigger.focus();
      await userEvent.keyboard("{Enter}");
      expect(queryTestId(container, "panel")).not.toBeNull();

      await userEvent.keyboard(" ");
      await expect.poll(() => queryTestId(container, "panel")).toBeNull();
    });
  });
});
