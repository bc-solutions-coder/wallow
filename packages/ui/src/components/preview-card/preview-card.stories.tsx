import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { PreviewCard } from "./preview-card";

/*
 * Wallow-m5aq.3.5 — PreviewCard stories. `@storybook/addon-vitest` turns every
 * export below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec while
 * preview-card.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * What belongs HERE rather than in preview-card.test.tsx:
 *
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens) — this project compiles real Tailwind and the
 *     `browser` project does not, so a recipe full of invented class names passes
 *     every class-set assertion over there and still paints nothing.
 *   - The POINTER journeys. `userEvent` here is `@testing-library/user-event`
 *     (bundled by `storybook/test`), which dispatches synthetic events straight at
 *     the element with no hit-testing and no real mouse, so hovering a trigger
 *     costs nothing and cannot leave the pointer parked over the next story's
 *     trigger the way the `browser` project's real Playwright mouse can.
 *
 * Two rules this file inherits, and one it adds:
 *
 *   - never assert `toBeVisible()` on a popup immediately after opening it (the
 *     Dialog exemplar's rule): the popup recipe carries a 150 ms enter transition
 *     starting at `opacity-0`, and a computed opacity of "0" scores as not
 *     visible, so the assertion races the animation the recipe is required to
 *     have. Wrap it in `waitFor`;
 *   - never assert `transform` for `rotate-45` (the Popover task's finding):
 *     Tailwind v4 emits the individual `rotate` property, and `transform` computes
 *     to "none";
 *   - EVERY interactive story sets `delay={0} closeDelay={0}` ON THE TRIGGER.
 *     Unlike a tooltip, a preview card applies its delay to the FOCUS path as well
 *     as the pointer one, and Base UI's default is 600 ms — a story that omitted
 *     it would be waiting out more than half a second before anything happened.
 *     `delay` lives on the trigger, not on the `Root` and not on a provider.
 */

interface ProfilePreviewCardProps {
  /** Opens the card on first render, for the screenshot stories. */
  readonly defaultOpen?: boolean;
  /** Renders the dimming scrim behind the card. */
  readonly withBackdrop?: boolean;
  /** Called with the card's new open state. */
  readonly onOpenChange?: (open: boolean) => void;
}

/**
 * A complete, realistic preview card — the story subject. Stories drive the real
 * `PreviewCard` namespace through this so every part is exercised together
 * rather than one part at a time.
 *
 * The trigger sits INSIDE A SENTENCE rather than standing alone, because that is
 * what a preview card trigger is: a link in running prose whose underline firms
 * up while its card is on screen. It needs a real `href` — measured, a Base UI
 * preview-card trigger without one is not focusable at all.
 */
function ProfilePreviewCard({
  defaultOpen,
  withBackdrop,
  onOpenChange,
}: ProfilePreviewCardProps): ReactElement {
  return (
    <div className="flex min-h-64 items-center justify-center p-8">
      <p className="max-w-md text-foreground">
        The first published algorithm intended for a machine was written by{" "}
        <PreviewCard.Root defaultOpen={defaultOpen} onOpenChange={onOpenChange}>
          <PreviewCard.Trigger
            data-testid="profile-trigger"
            href="https://example.com/ada"
            delay={0}
            closeDelay={0}
          >
            Ada Lovelace
          </PreviewCard.Trigger>
          <PreviewCard.Portal>
            {withBackdrop ? <PreviewCard.Backdrop data-testid="profile-backdrop" /> : null}
            <PreviewCard.Positioner data-testid="profile-positioner" sideOffset={8}>
              <PreviewCard.Popup data-testid="profile-popup">
                <PreviewCard.Arrow data-testid="profile-arrow" />
                <PreviewCard.Viewport data-testid="profile-viewport">
                  <div className="flex flex-col gap-2">
                    <span className="text-sm font-semibold" data-testid="profile-name">
                      Ada Lovelace
                    </span>
                    <span className="text-sm text-muted-foreground">
                      Mathematician. Wrote the first algorithm for Babbage&apos;s Analytical Engine.
                    </span>
                    <a
                      className="text-sm underline underline-offset-4"
                      href="https://example.com/ada/notes"
                      data-testid="profile-notes"
                    >
                      Read the notes
                    </a>
                  </div>
                </PreviewCard.Viewport>
              </PreviewCard.Popup>
            </PreviewCard.Positioner>
          </PreviewCard.Portal>
        </PreviewCard.Root>{" "}
        in 1843.
      </p>
    </div>
  );
}

const meta = {
  title: "Components/PreviewCard",
  component: ProfilePreviewCard,
  args: {
    onOpenChange: fn(),
  },
} satisfies Meta<typeof ProfilePreviewCard>;

export default meta;

type Story = StoryObj<typeof meta>;

/**
 * The closed trigger in its sentence — the state a page shows most of the time,
 * and the only story that shows what the card costs the prose it lives in.
 */
export const Default: Story = {};

/** The open card: anchored popup, arrow and viewport contents. */
export const Open: Story = {
  args: { defaultOpen: true },
};

/**
 * The same card with the optional dimming scrim. Lighter than the popover's and
 * far lighter than the dialog's, and — unlike either — it cannot be pressed:
 * Base UI gives this element `pointer-events: none` inline, so it dims and
 * nothing else.
 */
export const WithBackdrop: Story = {
  args: { defaultOpen: true, withBackdrop: true },
};

/**
 * The controlled shape: the open state lives in the caller's `useState`, and the
 * card reports every change back through `onOpenChange`. This is the story a
 * consumer copies when the card has to open from somewhere other than its own
 * trigger.
 *
 * NOTE THE TWO EXPLICIT BUTTONS RATHER THAN ONE TOGGLE — this is a real trap,
 * measured while writing this story. A `setOpen(previous => !previous)` button
 * placed OUTSIDE an open card cannot work: pressing it is also an outside press,
 * so Base UI fires `onOpenChange(false)` and closes the card FIRST, and the
 * button's own handler then reads the already-false state and flips it straight
 * back to open. The card appears stuck open on every second press. Idempotent
 * `setOpen(true)` / `setOpen(false)` controls have no such ordering to get wrong,
 * which is why a consumer should copy this shape and not a toggle.
 */
export const Controlled: Story = {
  render: function ControlledPreviewCard(args) {
    const [open, setOpen] = useState(false);

    return (
      <div className="flex flex-col gap-2 p-8">
        <span data-testid="controlled-state">{open ? "open" : "closed"}</span>
        <button
          type="button"
          data-testid="controlled-open"
          onClick={() => {
            setOpen(true);
          }}
        >
          Open from outside
        </button>
        <button
          type="button"
          data-testid="controlled-close"
          onClick={() => {
            setOpen(false);
          }}
        >
          Close from outside
        </button>
        <PreviewCard.Root
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            args.onOpenChange?.(next);
          }}
        >
          <PreviewCard.Trigger
            data-testid="controlled-trigger"
            href="https://example.com/ada"
            delay={0}
            closeDelay={0}
          >
            Ada Lovelace
          </PreviewCard.Trigger>
          <PreviewCard.Portal>
            <PreviewCard.Positioner sideOffset={8}>
              <PreviewCard.Popup data-testid="controlled-popup">
                <PreviewCard.Arrow />
                Mathematician, 1815&ndash;1852.
              </PreviewCard.Popup>
            </PreviewCard.Positioner>
          </PreviewCard.Portal>
        </PreviewCard.Root>
      </div>
    );
  },
  play: async ({ canvas }) => {
    // Opened from a control that is not the trigger — the whole point of the
    // controlled shape, and something a hover-driven card cannot do on its own.
    await userEvent.click(canvas.getByTestId("controlled-open"));

    const popup = await screen.findByTestId("controlled-popup");
    await expect(popup).toHaveAttribute("data-open");
    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("open");

    await userEvent.click(canvas.getByTestId("controlled-close"));

    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("closed");
    await waitFor(async () => {
      await expect(screen.queryByTestId("controlled-popup")).not.toBeInTheDocument();
    });
  },
};

/**
 * The journey a preview card exists for: point at the link, read the card, point
 * away. Synthetic events, so no real mouse is left parked anywhere.
 */
export const OpenOnHover: Story = {
  play: async ({ args, canvas }) => {
    const trigger = canvas.getByTestId("profile-trigger");

    await userEvent.hover(trigger);

    // Portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("profile-popup");
    // The popup recipe carries a 150ms enter transition starting at opacity-0,
    // so it is not "visible" until that settles.
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(popup).toHaveAttribute("data-open");
    await expect(trigger).toHaveAttribute("data-popup-open");
    await expect(args.onOpenChange).toHaveBeenCalledWith(true, expect.anything());

    await userEvent.unhover(trigger);

    await waitFor(async () => {
      await expect(screen.queryByTestId("profile-popup")).not.toBeInTheDocument();
    });
    await expect(trigger).not.toHaveAttribute("data-popup-open");
  },
};

/**
 * Pressing anywhere outside dismisses the card — and the press deliberately
 * lands on ORDINARY PAGE TEXT rather than on the backdrop, because this
 * component's backdrop carries `pointer-events: none` inline and can never
 * receive one. That is the difference from `Popover`'s
 * DismissOnBackdropPress story, and the reason this story renders the scrim while
 * pressing past it.
 */
export const DismissOnOutsidePress: Story = {
  args: { defaultOpen: true, withBackdrop: true },
  play: async ({ canvasElement }) => {
    await screen.findByTestId("profile-popup");

    await userEvent.click(canvasElement.querySelector("p") as HTMLElement);

    await waitFor(async () => {
      await expect(screen.queryByTestId("profile-popup")).not.toBeInTheDocument();
    });
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes preview-card.test.tsx's class-set assertions and still
 * paints nothing. These assertions read computed styles instead of class names.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { defaultOpen: true, withBackdrop: true },
  play: async ({ canvas }) => {
    const backdrop = await screen.findByTestId("profile-backdrop");
    const positioner = await screen.findByTestId("profile-positioner");
    const popup = await screen.findByTestId("profile-popup");
    const arrow = await screen.findByTestId("profile-arrow");

    // `fixed inset-0 z-40 bg-foreground/10` on the backdrop, against the
    // unstyled defaults of `static` and a transparent background. `z-40` is
    // asserted exactly, because the scrim sitting ABOVE the card it dims is the
    // one way this recipe can be wrong and still look plausible.
    const backdropStyle = getComputedStyle(backdrop);
    await expect(backdropStyle.position).toBe("fixed");
    await expect(backdropStyle.zIndex).toBe("40");
    await expect(backdropStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");

    // The positioner contributes stacking only; Base UI supplies its placement.
    const positionerStyle = getComputedStyle(positioner);
    await expect(positionerStyle.position).toBe("absolute");
    await expect(positionerStyle.zIndex).toBe("50");

    // `w-72`, `bg-popover`, `border border-border`, `p-4` and `rounded-lg` on the
    // popup — and no placement of its own. The width is exact: a preview card's
    // fixed width is part of its format, so `w-72` losing to a stray `max-w-*`
    // has to fail here.
    const popupStyle = getComputedStyle(popup);
    await expect(popupStyle.width).toBe("288px");
    await expect(popupStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(popupStyle.borderTopWidth).not.toBe("0px");
    await expect(popupStyle.borderTopStyle).toBe("solid");
    await expect(popupStyle.paddingTop).not.toBe("0px");
    await expect(popupStyle.borderTopLeftRadius).not.toBe("0px");

    // The arrow: `h-2.5 w-2.5` gives it a real box, `rotate-45` a 45deg rotate,
    // and `bg-popover`/`border-border` the popup's own surface tokens rather than
    // a hardcoded colour. Tailwind v4 emits the INDIVIDUAL `rotate` property, so
    // `transform` computes to "none" — never assert it.
    const arrowStyle = getComputedStyle(arrow);
    await expect(arrowStyle.position).toBe("absolute");
    await expect(arrowStyle.width).toBe("10px");
    await expect(arrowStyle.height).toBe("10px");
    await expect(arrowStyle.rotate).toBe("45deg");
    await expect(arrowStyle.backgroundColor).toBe(popupStyle.backgroundColor);
    await expect(arrowStyle.borderTopColor).toBe(popupStyle.borderTopColor);

    // `relative overflow-hidden` on the viewport, so the cross-fade has a
    // positioning context to run inside.
    const viewportStyle = getComputedStyle(await screen.findByTestId("profile-viewport"));
    await expect(viewportStyle.position).toBe("relative");
    await expect(viewportStyle.overflow).toBe("hidden");

    // The trigger is the one part styled in BOTH states, and the only proof that
    // the `data-[popup-open]:` modifier compiles and matches: dotted while the
    // card is closed, solid while it is open. The card is open here, so the
    // second half is read now and the first from a closed sibling trigger.
    const triggerStyle = getComputedStyle(canvas.getByTestId("profile-trigger"));
    await expect(triggerStyle.textDecorationLine).toBe("underline");
    await expect(triggerStyle.textDecorationStyle).toBe("solid");
    await expect(triggerStyle.textUnderlineOffset).not.toBe("auto");
  },
};

/**
 * The closed half of the trigger's two-state styling, split out so
 * PaintedByTheDesignTokens can read the open half: the underline is DOTTED until
 * the card appears. Together the two stories prove
 * `data-[popup-open]:decoration-solid` both compiles and matches.
 */
export const TriggerUnderlineWhileClosed: Story = {
  play: async ({ canvas }) => {
    const triggerStyle = getComputedStyle(canvas.getByTestId("profile-trigger"));

    await expect(triggerStyle.textDecorationLine).toBe("underline");
    await expect(triggerStyle.textDecorationStyle).toBe("dotted");
  },
};
