import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { Toolbar } from "./toolbar";

/*
 * Wallow-m5aq.4.5 — Toolbar. Same spec shape as the Wave-1 exemplar
 * (Wallow-m5aq.2.1) and the Wave-2 exemplar (Wallow-m5aq.3.1): browser vitest
 * project, nothing mocked, the recipes asserted THROUGH the component, class
 * assertions as an order-free set.
 *
 * Nothing here is portalled and nothing here is a popup, so every query goes
 * through render()'s `container` and none of the Wave-2 overlay gotchas apply.
 *
 * ANATOMY, measured against the installed Base UI 1.6.0 rather than read off the
 * docs (a throwaway probe spec, since deleted):
 *
 *   <div role="toolbar" aria-orientation data-orientation>            <- Root
 *     <button type="button" data-focusable aria-disabled tabindex>     <- Button
 *     <div role="separator" data-orientation aria-orientation>         <- Separator
 *     <div role="group" data-orientation>                              <- Group
 *     <input data-focusable tabindex>                                  <- Input
 *     <a tabindex href>                                                <- Link
 *
 * Four measurements are worth stating, because each is easy to assume wrong:
 *   - THE SEPARATOR'S ORIENTATION IS THE OPPOSITE OF THE TOOLBAR'S. A horizontal
 *     toolbar renders `data-orientation="vertical"` rules and vice versa, because
 *     a rule crosses the strip. The recipe's `data-[orientation=vertical]:` arm
 *     is therefore the one that fires in the DEFAULT horizontal toolbar — the
 *     reverse of every other part here, and the reverse of the catalog's
 *     standalone Separator.
 *   - `focusableWhenDisabled` DEFAULTS TO TRUE. A disabled button therefore keeps
 *     `data-disabled` + `aria-disabled="true"`, stays in the arrow-key order, and
 *     carries NO native `disabled` attribute. Opting out (`focusableWhenDisabled
 *     ={false}`) swaps that for a real `disabled` attribute and drops the item
 *     out of the roving order entirely.
 *   - *** NEVER `userEvent.click()` A DISABLED TOOLBAR BUTTON. *** Playwright's
 *     actionability check treats `aria-disabled="true"` as not-enabled and blocks
 *     the click until the 15s timeout. The click must be dispatched with the DOM
 *     `element.click()` instead — the same device the Wave-2 pointer-blocker
 *     gotcha established.
 *   - A `Toolbar.Input` SWALLOWS THE HORIZONTAL ARROW KEY while its caret can
 *     still move inside its own value. With a non-empty value, the first
 *     ArrowRight moves the caret and only the next one leaves the field; with an
 *     empty value it exits on the first press. The keyboard specs below use an
 *     EMPTY input so the step count is the obvious one.
 *
 * Base UI stamps NO class of its own on any of the six parts (probed), so every
 * class set below is pure recipe and is asserted with no spread-in extras.
 */

/** Every utility `Toolbar.Root` must render. Single source of truth. */
const ROOT_CLASSES = [
  "flex",
  "items-center",
  "gap-1",
  "rounded-md",
  "border",
  "border-border",
  "bg-card",
  "p-1",
  "data-[orientation=vertical]:flex-col",
  "data-[orientation=vertical]:items-stretch",
  "data-[disabled]:opacity-50",
];

/** Every utility `Toolbar.Group` must render. */
const GROUP_CLASSES = [
  "flex",
  "items-center",
  "gap-1",
  "data-[orientation=vertical]:flex-col",
  "data-[orientation=vertical]:items-stretch",
];

/**
 * Every utility `Toolbar.Button` must render. The disabled treatment hangs off
 * Base UI's `data-disabled` rather than `:disabled`, because a disabled toolbar
 * button is `aria-disabled` and NOT natively disabled by default.
 */
const BUTTON_CLASSES = [
  "inline-flex",
  "items-center",
  "justify-center",
  "gap-2",
  "whitespace-nowrap",
  "rounded-sm",
  "px-2.5",
  "py-1.5",
  "text-sm",
  "font-medium",
  "text-foreground",
  "transition-colors",
  "hover:bg-accent",
  "hover:text-accent-foreground",
  "focus-visible:ring-2",
  "focus-visible:ring-ring",
  "outline-none",
  "data-[disabled]:opacity-50",
];

/** Every utility `Toolbar.Link` must render. */
const LINK_CLASSES = [
  "inline-flex",
  "items-center",
  "gap-1",
  "rounded-sm",
  "px-2.5",
  "py-1.5",
  "text-sm",
  "font-medium",
  "text-primary",
  "underline-offset-4",
  "hover:underline",
  "focus-visible:ring-2",
  "focus-visible:ring-ring",
  "outline-none",
];

/** Every utility `Toolbar.Input` must render. */
const INPUT_CLASSES = [
  "h-8",
  "rounded-sm",
  "border",
  "border-border",
  "bg-background",
  "px-2",
  "text-sm",
  "text-foreground",
  "focus-visible:ring-2",
  "focus-visible:ring-ring",
  "outline-none",
  "data-[disabled]:opacity-50",
];

/**
 * Every utility `Toolbar.Separator` must render. Note which arm fires by
 * default: in a HORIZONTAL toolbar the rule is VERTICAL, so
 * `data-[orientation=vertical]:` is the everyday case here. The rule is a fixed
 * `h-5` rather than the standalone Separator's `h-full`, because the strip is
 * `items-center` and a full-height rule would collapse to nothing.
 */
const SEPARATOR_CLASSES = [
  "shrink-0",
  "bg-border",
  "data-[orientation=vertical]:h-5",
  "data-[orientation=vertical]:w-px",
  "data-[orientation=horizontal]:h-px",
  "data-[orientation=horizontal]:w-full",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function part(container: HTMLElement, testId: string): HTMLElement {
  const element = container.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
  expect(element, testId).not.toBeNull();
  return element as HTMLElement;
}

interface FixtureProps {
  readonly orientation?: "horizontal" | "vertical";
  readonly disabled?: boolean;
  readonly loopFocus?: boolean;
  readonly onBoldClick?: () => void;
  readonly onItalicClick?: () => void;
  /** Renders the italic button disabled (still focusable, by Base UI's default). */
  readonly italicDisabled?: boolean;
  /** Drops the italic button out of the roving order entirely. */
  readonly italicFocusableWhenDisabled?: boolean;
  readonly boldClassName?: string;
}

/**
 * The formatting-strip fixture every case below starts from. The input is left
 * EMPTY on purpose (see the caret gotcha in the header), and the roving order is
 * bold -> italic -> input -> link.
 */
function renderToolbar({
  orientation,
  disabled,
  loopFocus,
  onBoldClick,
  onItalicClick,
  italicDisabled,
  italicFocusableWhenDisabled,
  boldClassName,
}: FixtureProps = {}) {
  return render(
    <Toolbar.Root
      data-testid="toolbar"
      orientation={orientation}
      disabled={disabled}
      loopFocus={loopFocus}
    >
      <Toolbar.Group data-testid="group">
        <Toolbar.Button data-testid="bold" className={boldClassName} onClick={onBoldClick}>
          Bold
        </Toolbar.Button>
        <Toolbar.Button
          data-testid="italic"
          onClick={onItalicClick}
          disabled={italicDisabled}
          focusableWhenDisabled={italicFocusableWhenDisabled}
        >
          Italic
        </Toolbar.Button>
      </Toolbar.Group>
      <Toolbar.Separator data-testid="separator" />
      <Toolbar.Input data-testid="search" aria-label="Search" />
      <Toolbar.Link data-testid="help" href="https://example.com/help">
        Help
      </Toolbar.Link>
    </Toolbar.Root>,
  );
}

describe("Toolbar", () => {
  describe("anatomy", () => {
    it("mirrors Base UI's six namespace members exactly", () => {
      expect(Object.keys(Toolbar).toSorted()).toEqual([
        "Button",
        "Group",
        "Input",
        "Link",
        "Root",
        "Separator",
      ]);
    });

    it("renders the toolbar role and each part's own element", async () => {
      const { container } = await renderToolbar();

      expect(part(container, "toolbar").getAttribute("role")).toBe("toolbar");
      expect(part(container, "toolbar").getAttribute("aria-orientation")).toBe("horizontal");
      expect(part(container, "group").getAttribute("role")).toBe("group");
      expect(part(container, "bold").tagName).toBe("BUTTON");
      expect(part(container, "bold").getAttribute("type")).toBe("button");
      expect(part(container, "separator").getAttribute("role")).toBe("separator");
      expect(part(container, "search").tagName).toBe("INPUT");
      expect(part(container, "help").tagName).toBe("A");
      expect(part(container, "help").getAttribute("href")).toBe("https://example.com/help");
    });

    it("gives the separator the OPPOSITE orientation to the toolbar", async () => {
      // The rule crosses the strip, so a horizontal toolbar renders vertical
      // rules. The recipe's `data-[orientation=vertical]:` arm is what fires in
      // the everyday case — the reverse of the standalone Separator.
      const { container } = await renderToolbar();

      expect(part(container, "toolbar").getAttribute("data-orientation")).toBe("horizontal");
      expect(part(container, "separator").getAttribute("data-orientation")).toBe("vertical");
      expect(part(container, "separator").getAttribute("aria-orientation")).toBe("vertical");
    });

    it("flips every part, and the separator back again, when the toolbar is vertical", async () => {
      const { container } = await renderToolbar({ orientation: "vertical" });

      expect(part(container, "toolbar").getAttribute("data-orientation")).toBe("vertical");
      expect(part(container, "toolbar").getAttribute("aria-orientation")).toBe("vertical");
      expect(part(container, "group").getAttribute("data-orientation")).toBe("vertical");
      expect(part(container, "bold").getAttribute("data-orientation")).toBe("vertical");
      expect(part(container, "separator").getAttribute("data-orientation")).toBe("horizontal");
    });

    it("holds exactly one tab stop across the whole strip", async () => {
      // The point of a composite widget: the strip is ONE Tab stop, and the
      // arrow keys move within it.
      const { container } = await renderToolbar();

      expect(part(container, "bold").getAttribute("tabindex")).toBe("0");
      for (const testId of ["italic", "search", "help"]) {
        expect(part(container, testId).getAttribute("tabindex"), testId).toBe("-1");
      }
    });

    it("disables every item when the root is disabled, without removing them", async () => {
      const { container } = await renderToolbar({ disabled: true });

      expect(part(container, "toolbar").hasAttribute("data-disabled")).toBe(true);
      expect(part(container, "bold").hasAttribute("data-disabled")).toBe(true);
      expect(part(container, "bold").getAttribute("aria-disabled")).toBe("true");
    });
  });

  describe("recipes", () => {
    it("paints the root and the group", async () => {
      const { container } = await renderToolbar();

      expect(classSet(part(container, "toolbar"))).toEqual(ROOT_CLASSES.toSorted());
      expect(classSet(part(container, "group"))).toEqual(GROUP_CLASSES.toSorted());
    });

    it("paints every button the same, enabled or not", async () => {
      // One recipe for both states: disabled is a `data-[disabled]:` modifier
      // inside it, so the class SET never varies by state.
      const { container } = await renderToolbar({ italicDisabled: true });

      for (const testId of ["bold", "italic"]) {
        expect(classSet(part(container, testId)), testId).toEqual(BUTTON_CLASSES.toSorted());
      }
    });

    it("paints the link, the input and the separator", async () => {
      const { container } = await renderToolbar();

      expect(classSet(part(container, "help"))).toEqual(LINK_CLASSES.toSorted());
      expect(classSet(part(container, "search"))).toEqual(INPUT_CLASSES.toSorted());
      expect(classSet(part(container, "separator"))).toEqual(SEPARATOR_CLASSES.toSorted());
    });
  });

  describe("keyboard navigation", () => {
    it("moves the single tab stop along the strip with the arrow keys", async () => {
      const { container } = await renderToolbar();

      part(container, "bold").focus();
      await userEvent.keyboard("{ArrowRight}");

      expect(document.activeElement).toBe(part(container, "italic"));
      expect(part(container, "italic").getAttribute("tabindex")).toBe("0");
      expect(part(container, "bold").getAttribute("tabindex")).toBe("-1");
    });

    it("crosses a group boundary, the input and the link in one sweep", async () => {
      const { container } = await renderToolbar();

      part(container, "bold").focus();
      await userEvent.keyboard("{ArrowRight}");
      await userEvent.keyboard("{ArrowRight}");
      expect(document.activeElement).toBe(part(container, "search"));

      await userEvent.keyboard("{ArrowRight}");
      expect(document.activeElement).toBe(part(container, "help"));
    });

    it("wraps back to the first item at the end, by default", async () => {
      const { container } = await renderToolbar();

      part(container, "help").focus();
      await userEvent.keyboard("{ArrowRight}");

      expect(document.activeElement).toBe(part(container, "bold"));
    });

    it("stops at the last item when the caller turns loopFocus off", async () => {
      const { container } = await renderToolbar({ loopFocus: false });

      part(container, "help").focus();
      await userEvent.keyboard("{ArrowRight}");

      expect(document.activeElement).toBe(part(container, "help"));
    });

    it("follows the vertical axis when the toolbar is vertical", async () => {
      const { container } = await renderToolbar({ orientation: "vertical" });

      part(container, "bold").focus();
      await userEvent.keyboard("{ArrowDown}");

      expect(document.activeElement).toBe(part(container, "italic"));
    });

    it("keeps a disabled item reachable by arrow, by default", async () => {
      // `focusableWhenDisabled` defaults to TRUE: a disabled control stays
      // discoverable to a keyboard user instead of vanishing from the order.
      const { container } = await renderToolbar({ italicDisabled: true });

      part(container, "bold").focus();
      await userEvent.keyboard("{ArrowRight}");

      expect(document.activeElement).toBe(part(container, "italic"));
      expect(part(container, "italic").hasAttribute("data-disabled")).toBe(true);
      expect(part(container, "italic").getAttribute("aria-disabled")).toBe("true");
      expect(part(container, "italic").hasAttribute("disabled")).toBe(false);
    });

    it("skips an item that opts out of focusableWhenDisabled", async () => {
      const { container } = await renderToolbar({
        italicDisabled: true,
        italicFocusableWhenDisabled: false,
      });

      part(container, "bold").focus();
      await userEvent.keyboard("{ArrowRight}");

      expect(document.activeElement).toBe(part(container, "search"));
      // Opting out swaps aria-disabled for the REAL disabled attribute.
      expect(part(container, "italic").hasAttribute("disabled")).toBe(true);
    });
  });

  describe("activation", () => {
    it("fires a click on an enabled button", async () => {
      const onBoldClick = vi.fn();
      const { container } = await renderToolbar({ onBoldClick });

      await userEvent.click(part(container, "bold"));

      expect(onBoldClick).toHaveBeenCalledTimes(1);
    });

    it("swallows the click on a disabled button", async () => {
      // element.click(), NOT userEvent.click(): Playwright refuses to act on an
      // aria-disabled control and would time out instead of failing usefully.
      const onItalicClick = vi.fn();
      const { container } = await renderToolbar({ onItalicClick, italicDisabled: true });

      part(container, "italic").click();

      expect(onItalicClick).not.toHaveBeenCalled();
    });

    it("activates the focused button on Enter", async () => {
      const onItalicClick = vi.fn();
      const { container } = await renderToolbar({ onItalicClick });

      part(container, "bold").focus();
      await userEvent.keyboard("{ArrowRight}");
      await userEvent.keyboard("{Enter}");

      expect(onItalicClick).toHaveBeenCalledTimes(1);
    });
  });

  describe("composition", () => {
    it("lets a caller className override a recipe utility on a button", async () => {
      // The cn()/tailwind-merge proof for this component: the conflicting
      // font-size utility is REMOVED rather than appended after, while the
      // utilities the caller never mentioned survive.
      const { container } = await renderToolbar({ boldClassName: "text-base" });

      const bold = part(container, "bold");
      expect(bold.classList.contains("text-base")).toBe(true);
      expect(bold.classList.contains("text-sm")).toBe(false);
      expect(bold.classList.contains("text-foreground")).toBe(true);
      expect(bold.classList.contains("data-[disabled]:opacity-50")).toBe(true);
    });

    it("composes the root recipe onto another element through the render prop", async () => {
      const { container } = await render(
        <Toolbar.Root render={<nav data-testid="toolbar" />}>
          <Toolbar.Button data-testid="bold">Bold</Toolbar.Button>
        </Toolbar.Root>,
      );

      const nav = container.querySelector("nav");
      expect(nav).not.toBeNull();
      expect(classSet(nav as Element)).toEqual(ROOT_CLASSES.toSorted());
    });

    it("passes an app-owned aria-label and data-testid through to the root", async () => {
      const { container } = await render(
        <Toolbar.Root data-testid="editor-toolbar" aria-label="Text formatting">
          <Toolbar.Button>Bold</Toolbar.Button>
        </Toolbar.Root>,
      );

      expect(part(container, "editor-toolbar").getAttribute("aria-label")).toBe("Text formatting");
    });
  });
});
