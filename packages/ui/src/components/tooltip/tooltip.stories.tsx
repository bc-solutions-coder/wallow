import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { Tooltip } from "./tooltip";

/*
 * Wallow-m5aq.3.4 — Tooltip stories. `@storybook/addon-vitest` turns every export
 * below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while tooltip.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Two things belong HERE rather than in tooltip.test.tsx:
 *
 *   - HOVER interaction. `userEvent` here is `@testing-library/user-event`
 *     (bundled by `storybook/test`), which dispatches synthetic pointer events
 *     at the element rather than driving the real Playwright mouse, so hovering
 *     costs nothing and leaves no pointer position behind to contaminate the
 *     next story. tooltip.test.tsx opens through focus for exactly that reason
 *     and keeps its single real-mouse spec last in the file.
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens) — this project compiles real Tailwind and the
 *     `browser` project does not.
 *
 * Note the rule the Dialog exemplar closed out (gotcha 6): never assert
 * `toBeVisible()` on a popup straight after opening it. The popup recipe carries
 * a 150 ms enter transition starting at `opacity-0`, and jest-dom scores a
 * computed opacity of "0" as not visible, so the assertion would race the very
 * animation the recipe is required to have. These stories assert presence and
 * `data-open` instead, and leave the visual proof to PaintedByTheDesignTokens.
 */

interface SaveTooltipProps {
  /** Opens the tooltip on first render, for the screenshot stories. */
  readonly defaultOpen?: boolean;
  /** Renders the pointer triangle between the trigger and the bubble. */
  readonly withArrow?: boolean;
  /** The shared open delay, in milliseconds, handed to `Tooltip.Provider`. */
  readonly delay?: number;
  /** Called with the tooltip's new open state. */
  readonly onOpenChange?: (open: boolean) => void;
}

/**
 * A complete, realistic tooltip — the story subject. Stories drive the real
 * `Tooltip` namespace through this so every part is exercised together rather
 * than one part at a time, including the `Provider` a fork would mount once near
 * the root of its app.
 */
function SaveTooltip({
  defaultOpen,
  withArrow,
  delay = 0,
  onOpenChange,
}: SaveTooltipProps): ReactElement {
  return (
    <Tooltip.Provider delay={delay} closeDelay={0}>
      <Tooltip.Root defaultOpen={defaultOpen} onOpenChange={onOpenChange}>
        <Tooltip.Trigger data-testid="save-trigger">Save draft</Tooltip.Trigger>
        <Tooltip.Portal>
          <Tooltip.Positioner data-testid="save-positioner" sideOffset={8}>
            <Tooltip.Popup data-testid="save-popup">
              {withArrow ? <Tooltip.Arrow data-testid="save-arrow">▾</Tooltip.Arrow> : null}
              <Tooltip.Viewport data-testid="save-viewport">
                Saves your work without publishing it
              </Tooltip.Viewport>
            </Tooltip.Popup>
          </Tooltip.Positioner>
        </Tooltip.Portal>
      </Tooltip.Root>
    </Tooltip.Provider>
  );
}

const meta = {
  title: "Components/Tooltip",
  component: SaveTooltip,
  args: {
    onOpenChange: fn(),
  },
} satisfies Meta<typeof SaveTooltip>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The resting state — a bare trigger, which is what a page shows most of the time. */
export const Default: Story = {};

/** The open bubble: positioner, popup and the viewport holding the copy. */
export const Open: Story = {
  args: { defaultOpen: true },
};

/** The same bubble with the pointer triangle Base UI positions against the anchor. */
export const WithArrow: Story = {
  args: { defaultOpen: true, withArrow: true },
};

/**
 * The controlled shape: the open state lives in the caller's `useState` and the
 * tooltip reports every change back through `onOpenChange`, so the caller's
 * state and the popup can never disagree.
 *
 * Note what the external control does and does not do. It can CLOSE the
 * tooltip, and it starts open to show that the caller's state is what decides.
 * It cannot OPEN one: Base UI dismisses a tooltip on any press outside the
 * popup (measured — `onOpenChange(false, { reason: "outside-press" })`), so a
 * click on an outside button opens the tooltip through the caller's handler and
 * dismisses it again within the same gesture. A tooltip is opened by its
 * trigger; see OpensOnHover and OpensOnFocus.
 */
export const Controlled: Story = {
  render: function ControlledTooltip(args) {
    const [open, setOpen] = useState(true);

    return (
      <div className="flex flex-col gap-2">
        <button
          type="button"
          data-testid="controlled-external"
          className="text-sm text-foreground"
          onClick={() => setOpen(false)}
        >
          Dismiss the tooltip from outside
        </button>
        <span data-testid="controlled-state">{open ? "open" : "closed"}</span>
        <Tooltip.Root
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            args.onOpenChange?.(next);
          }}
        >
          <Tooltip.Trigger data-testid="controlled-trigger">Save draft</Tooltip.Trigger>
          <Tooltip.Portal>
            <Tooltip.Positioner data-testid="controlled-positioner">
              <Tooltip.Popup data-testid="controlled-popup">The caller owns this</Tooltip.Popup>
            </Tooltip.Positioner>
          </Tooltip.Portal>
        </Tooltip.Root>
      </div>
    );
  },
  play: async ({ canvas }) => {
    // The popup is portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("controlled-popup");
    await expect(popup).toHaveAttribute("data-open");
    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("open");

    await userEvent.click(canvas.getByTestId("controlled-external"));

    // Wait for the close to land before reading the caller's own state: this
    // `userEvent` is synthetic and does not flush React for us, so asserting the
    // text straight after the click races the re-render.
    await waitFor(async () => {
      await expect(screen.queryByTestId("controlled-popup")).not.toBeInTheDocument();
    });
    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("closed");
  },
};

/**
 * The pointer path. Base UI renders no blocker inside a tooltip's portal, and
 * `Tooltip.Provider`'s `delay={0}` removes the 600 ms wait Base UI applies by
 * default, so a hover shows the bubble straight away and leaving the trigger
 * takes it away again.
 */
export const OpensOnHover: Story = {
  play: async ({ args, canvas }) => {
    const trigger = canvas.getByTestId("save-trigger");

    await userEvent.hover(trigger);

    const popup = await screen.findByTestId("save-popup");
    await expect(popup).toHaveAttribute("data-open");
    await expect(trigger).toHaveAttribute("data-popup-open");
    await expect(args.onOpenChange).toHaveBeenCalledWith(true, expect.anything());

    await userEvent.unhover(trigger);

    await waitFor(async () => {
      await expect(screen.queryByTestId("save-popup")).not.toBeInTheDocument();
    });
    await expect(trigger).not.toHaveAttribute("data-popup-open");
  },
};

/**
 * The keyboard path, which is the one that makes a tooltip usable at all for
 * anyone not holding a mouse: focusing the trigger shows the bubble, Escape
 * dismisses it without moving focus.
 */
export const OpensOnFocus: Story = {
  play: async ({ canvas }) => {
    const trigger = canvas.getByTestId("save-trigger");

    trigger.focus();

    const popup = await screen.findByTestId("save-popup");
    await expect(popup).toHaveAttribute("data-open");
    // Base UI skips the enter animation for a focus-driven open.
    await expect(popup).toHaveAttribute("data-instant", "focus");

    await userEvent.keyboard("{Escape}");

    await waitFor(async () => {
      await expect(screen.queryByTestId("save-popup")).not.toBeInTheDocument();
    });
    await expect(trigger).toHaveFocus();
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes tooltip.test.tsx's class-set assertions and still paints
 * nothing. These assertions read computed styles instead of class names.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { defaultOpen: true, withArrow: true },
  play: async () => {
    // `z-50` on the positioner, against an unstyled default of `auto`. The
    // positioner's `position` proves nothing — Base UI sets that inline.
    const positioner = await screen.findByTestId("save-positioner");
    await expect(getComputedStyle(positioner).zIndex).toBe("50");

    // `rounded-md border border-border bg-popover px-3 py-1.5 text-xs` on the
    // bubble, against the transparent, borderless, 16px defaults.
    const popupStyle = getComputedStyle(await screen.findByTestId("save-popup"));
    await expect(popupStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(popupStyle.borderTopWidth).not.toBe("0px");
    await expect(popupStyle.borderTopStyle).toBe("solid");
    await expect(popupStyle.borderTopLeftRadius).not.toBe("0px");
    await expect(popupStyle.paddingLeft).not.toBe("0px");
    await expect(popupStyle.fontSize).not.toBe("16px");

    // `flex` on the arrow and `relative overflow-hidden` on the viewport, both
    // against the plain `block`/`visible` defaults of a bare <div>.
    await expect(getComputedStyle(await screen.findByTestId("save-arrow")).display).toBe("flex");

    const viewportStyle = getComputedStyle(await screen.findByTestId("save-viewport"));
    await expect(viewportStyle.position).toBe("relative");
    await expect(viewportStyle.overflow).toBe("hidden");
  },
};
