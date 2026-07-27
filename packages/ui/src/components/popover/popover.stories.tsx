import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { Popover } from "./popover";

/*
 * Wallow-m5aq.3.3 — Popover stories. `@storybook/addon-vitest` turns every export
 * below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while popover.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Two things belong HERE rather than in popover.test.tsx:
 *
 *   - PRESSING THE BACKDROP. The backdrop carries no inline geometry, so in the
 *     `browser` project — which compiles no Tailwind — its `fixed inset-0` recipe
 *     class does nothing, it has no stable box, and Playwright's actionability
 *     check never resolves. `userEvent` here is `@testing-library/user-event`
 *     (bundled by `storybook/test`), which dispatches synthetic events straight
 *     at the element with no hit-testing at all. (Clicks INSIDE the popup do work
 *     in the `browser` project — a popover is non-modal, so there is no pointer
 *     blocker over it. That is where this file differs from dialog.stories.tsx.)
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens) — this project compiles real Tailwind and the
 *     `browser` project does not.
 *
 * One rule inherited from the Dialog exemplar: never assert `toBeVisible()` on a
 * popup immediately after opening it. The popup recipe carries a 150ms enter
 * transition starting at `opacity-0`, and jest-dom scores a computed opacity of
 * "0" as not visible, so the assertion races the animation the recipe is required
 * to have. Wrap it in `waitFor`.
 */

interface SharePopoverProps {
  /** Opens the popover on first render, for the screenshot stories. */
  readonly defaultOpen?: boolean;
  /** Renders an email field inside the popup, so the form layout is covered. */
  readonly withForm?: boolean;
  /** Called with the popover's new open state. */
  readonly onOpenChange?: (open: boolean) => void;
}

/**
 * A complete, realistic popover — the story subject. Stories drive the real
 * `Popover` namespace through this so every part is exercised together rather
 * than one part at a time.
 */
function SharePopover({ defaultOpen, withForm, onOpenChange }: SharePopoverProps): ReactElement {
  return (
    <div className="flex min-h-64 items-center justify-center">
      <Popover.Root defaultOpen={defaultOpen} onOpenChange={onOpenChange}>
        <Popover.Trigger data-testid="share-trigger">Share</Popover.Trigger>
        <Popover.Portal>
          <Popover.Backdrop data-testid="share-backdrop" />
          <Popover.Positioner data-testid="share-positioner" sideOffset={8}>
            <Popover.Popup data-testid="share-popup">
              <Popover.Arrow data-testid="share-arrow" />
              <Popover.Viewport data-testid="share-viewport">
                <Popover.Title data-testid="share-title">Share this project</Popover.Title>
                <Popover.Description data-testid="share-description">
                  Anyone with the link can view it.
                </Popover.Description>
                {withForm ? (
                  <form className="mt-4 flex flex-col gap-2">
                    <label className="text-sm" htmlFor="share-email">
                      Invite by email
                    </label>
                    <input
                      id="share-email"
                      data-testid="share-email"
                      className="rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground"
                    />
                  </form>
                ) : null}
                <div className="mt-4 flex justify-end">
                  <Popover.Close data-testid="share-close">Done</Popover.Close>
                </div>
              </Popover.Viewport>
            </Popover.Popup>
          </Popover.Positioner>
        </Popover.Portal>
      </Popover.Root>
    </div>
  );
}

const meta = {
  title: "Components/Popover",
  component: SharePopover,
  args: {
    onOpenChange: fn(),
  },
} satisfies Meta<typeof SharePopover>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The closed trigger — the state a page shows most of the time. */
export const Default: Story = {};

/** The open popover: anchored card, arrow, title, description and close. */
export const Open: Story = {
  args: { defaultOpen: true },
};

/** A popover whose body is a form rather than plain copy. */
export const WithForm: Story = {
  args: { defaultOpen: true, withForm: true },
};

/**
 * The controlled shape: the open state lives in the caller's `useState`, and the
 * popover reports every change back through `onOpenChange`. This is the story a
 * consumer copies when the popover has to open from somewhere other than its own
 * trigger.
 */
export const Controlled: Story = {
  render: function ControlledPopover(args) {
    const [open, setOpen] = useState(false);

    return (
      <div className="flex flex-col gap-2">
        <span data-testid="controlled-state">{open ? "open" : "closed"}</span>
        <Popover.Root
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            args.onOpenChange?.(next);
          }}
        >
          <Popover.Trigger data-testid="controlled-trigger">Share</Popover.Trigger>
          <Popover.Portal>
            <Popover.Positioner sideOffset={8}>
              <Popover.Popup data-testid="controlled-popup">
                <Popover.Arrow />
                <Popover.Title>Controlled</Popover.Title>
                <Popover.Description>The caller owns the open state.</Popover.Description>
                <Popover.Close data-testid="controlled-close">Done</Popover.Close>
              </Popover.Popup>
            </Popover.Positioner>
          </Popover.Portal>
        </Popover.Root>
      </div>
    );
  },
  play: async ({ canvas }) => {
    await userEvent.click(canvas.getByTestId("controlled-trigger"));

    const popup = await screen.findByTestId("controlled-popup");
    await expect(popup).toHaveAttribute("data-open");
    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("open");

    await userEvent.click(screen.getByTestId("controlled-close"));

    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("closed");
    await waitFor(async () => {
      await expect(screen.queryByTestId("controlled-popup")).not.toBeInTheDocument();
    });
  },
};

/** The interaction half: opening from the trigger, then dismissing. */
export const OpenAndDismiss: Story = {
  play: async ({ args, canvas }) => {
    const trigger = canvas.getByTestId("share-trigger");

    await userEvent.click(trigger);

    // The popup is portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("share-popup");
    // The popup recipe carries a 150ms enter transition starting at opacity-0,
    // so it is not "visible" until that settles.
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(popup).toHaveAttribute("data-open");
    await expect(trigger).toHaveAttribute("data-popup-open");
    await expect(args.onOpenChange).toHaveBeenCalledWith(true, expect.anything());

    await userEvent.click(screen.getByTestId("share-close"));

    await waitFor(async () => {
      await expect(screen.queryByTestId("share-popup")).not.toBeInTheDocument();
    });
    await expect(trigger).not.toHaveAttribute("data-popup-open");
  },
};

/** Pressing the backdrop dismisses the popover — the outside-press path. */
export const DismissOnBackdropPress: Story = {
  args: { defaultOpen: true },
  play: async () => {
    const backdrop = await screen.findByTestId("share-backdrop");

    await userEvent.click(backdrop);

    await waitFor(async () => {
      await expect(screen.queryByTestId("share-popup")).not.toBeInTheDocument();
    });
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes popover.test.tsx's class-set assertions and still paints
 * nothing. These assertions read computed styles instead of class names, and
 * they are the acceptance proof for the styled Arrow part.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { defaultOpen: true },
  play: async () => {
    const backdrop = await screen.findByTestId("share-backdrop");
    const positioner = await screen.findByTestId("share-positioner");
    const popup = await screen.findByTestId("share-popup");
    const arrow = await screen.findByTestId("share-arrow");

    // `fixed inset-0 bg-foreground/20` on the backdrop, against the unstyled
    // defaults of `static` and a transparent background.
    await expect(getComputedStyle(backdrop).position).toBe("fixed");
    await expect(getComputedStyle(backdrop).backgroundColor).not.toBe("rgba(0, 0, 0, 0)");

    // The positioner contributes stacking only; Base UI supplies its placement.
    const positionerStyle = getComputedStyle(positioner);
    await expect(positionerStyle.position).toBe("absolute");
    await expect(positionerStyle.zIndex).toBe("50");

    // `bg-popover`, `border border-border`, `p-4` and `rounded-lg` on the popup —
    // and no placement of its own.
    const popupStyle = getComputedStyle(popup);
    await expect(popupStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(popupStyle.borderTopWidth).not.toBe("0px");
    await expect(popupStyle.borderTopStyle).toBe("solid");
    await expect(popupStyle.paddingTop).not.toBe("0px");
    await expect(popupStyle.borderTopLeftRadius).not.toBe("0px");

    // The arrow: `h-2.5 w-2.5` gives it a real box, `rotate-45` a 45deg rotate, and
    // `bg-popover`/`border-border` the popup's own surface tokens rather than a
    // hardcoded colour.
    const arrowStyle = getComputedStyle(arrow);
    await expect(arrowStyle.position).toBe("absolute");
    await expect(arrowStyle.width).toBe("10px");
    await expect(arrowStyle.height).toBe("10px");
    await expect(arrowStyle.rotate).toBe("45deg");
    await expect(arrowStyle.backgroundColor).toBe(popupStyle.backgroundColor);
    await expect(arrowStyle.borderTopColor).toBe(popupStyle.borderTopColor);

    // `relative overflow-hidden` on the viewport, so the cross-fade has a
    // positioning context to run inside.
    const viewportStyle = getComputedStyle(await screen.findByTestId("share-viewport"));
    await expect(viewportStyle.position).toBe("relative");
    await expect(viewportStyle.overflow).toBe("hidden");

    // `text-sm font-semibold` on the title, against the <h2> defaults the reset
    // flattens to the body size.
    const titleStyle = getComputedStyle(await screen.findByTestId("share-title"));
    await expect(titleStyle.fontSize).not.toBe("16px");
    await expect(Number(titleStyle.fontWeight)).toBeGreaterThan(400);
  },
};
