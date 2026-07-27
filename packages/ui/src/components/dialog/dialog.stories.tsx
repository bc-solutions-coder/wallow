import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { Dialog } from "./dialog";

/*
 * Wallow-m5aq.3.1 — Dialog stories. `@storybook/addon-vitest` turns every export
 * below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while dialog.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Two things belong HERE rather than in dialog.test.tsx:
 *
 *   - POINTER interaction inside the open popup. Base UI always renders a fixed,
 *     full-window pointer blocker inside the portal, and `vitest/browser`'s
 *     `userEvent` drives real Playwright input, which hit-tests the click point
 *     and therefore hits the blocker instead of the popup. `userEvent` here is
 *     `@testing-library/user-event` (bundled by `storybook/test`), which
 *     dispatches synthetic events straight at the element with no hit-testing,
 *     so clicking a part inside an open popup just works.
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens) — this project compiles real Tailwind and the
 *     `browser` project does not.
 */

interface ConfirmDialogProps {
  /** Opens the dialog on first render, for the screenshot stories. */
  readonly defaultOpen?: boolean;
  /** Renders a name field inside the popup, so the form layout is covered. */
  readonly withForm?: boolean;
  /** Called with the dialog's new open state. */
  readonly onOpenChange?: (open: boolean) => void;
}

/**
 * A complete, realistic dialog — the story subject. Stories drive the real
 * `Dialog` namespace through this so every part is exercised together rather
 * than one part at a time.
 */
function ConfirmDialog({ defaultOpen, withForm, onOpenChange }: ConfirmDialogProps): ReactElement {
  return (
    <Dialog.Root defaultOpen={defaultOpen} onOpenChange={onOpenChange}>
      <Dialog.Trigger data-testid="confirm-trigger">Delete project</Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Backdrop data-testid="confirm-backdrop" />
        <Dialog.Popup data-testid="confirm-popup">
          <Dialog.Title data-testid="confirm-title">Delete project</Dialog.Title>
          <Dialog.Description data-testid="confirm-description">
            This permanently removes the project and everything in it.
          </Dialog.Description>
          {withForm ? (
            <form className="mt-4 flex flex-col gap-2">
              <label className="text-sm text-foreground" htmlFor="confirm-name">
                Type the project name to confirm
              </label>
              <input
                id="confirm-name"
                data-testid="confirm-name"
                className="rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground"
              />
            </form>
          ) : null}
          <div className="mt-6 flex justify-end gap-2">
            <Dialog.Close data-testid="confirm-cancel">Cancel</Dialog.Close>
          </div>
        </Dialog.Popup>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

const meta = {
  title: "Components/Dialog",
  component: ConfirmDialog,
  args: {
    onOpenChange: fn(),
  },
} satisfies Meta<typeof ConfirmDialog>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The closed trigger — the state a page shows most of the time. */
export const Default: Story = {};

/** The open dialog: backdrop, centred popup, title, description and close. */
export const Open: Story = {
  args: { defaultOpen: true },
};

/** A dialog whose body is a form rather than plain copy. */
export const WithForm: Story = {
  args: { defaultOpen: true, withForm: true },
};

/**
 * The controlled shape: the open state lives in the caller's `useState`, and the
 * dialog reports every change back through `onOpenChange`. This is the story a
 * consumer copies when the dialog has to open from somewhere other than its own
 * trigger.
 */
export const Controlled: Story = {
  render: function ControlledDialog(args) {
    const [open, setOpen] = useState(false);

    return (
      <div className="flex flex-col gap-2">
        <button
          type="button"
          data-testid="controlled-external"
          className="text-sm text-foreground"
          onClick={() => setOpen(true)}
        >
          Open from outside the dialog
        </button>
        <span data-testid="controlled-state">{open ? "open" : "closed"}</span>
        <Dialog.Root
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            args.onOpenChange?.(next);
          }}
        >
          <Dialog.Portal>
            <Dialog.Backdrop data-testid="controlled-backdrop" />
            <Dialog.Popup data-testid="controlled-popup">
              <Dialog.Title>Controlled</Dialog.Title>
              <Dialog.Description>The caller owns the open state.</Dialog.Description>
              <Dialog.Close data-testid="controlled-close">Close</Dialog.Close>
            </Dialog.Popup>
          </Dialog.Portal>
        </Dialog.Root>
      </div>
    );
  },
  play: async ({ canvas }) => {
    await userEvent.click(canvas.getByTestId("controlled-external"));

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
    const trigger = canvas.getByTestId("confirm-trigger");

    await userEvent.click(trigger);

    // The popup is portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("confirm-popup");
    // The popup recipe carries a 150ms enter transition starting at opacity-0,
    // so it is not "visible" until that settles.
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(popup).toHaveAttribute("data-open");
    await expect(trigger).toHaveAttribute("data-popup-open");
    await expect(args.onOpenChange).toHaveBeenCalledWith(true, expect.anything());

    // Real Tailwind is loaded here, so the popup's `z-50` puts it above Base
    // UI's own pointer blocker and a genuine click lands. The `browser` project
    // cannot do this — see the header.
    await userEvent.click(screen.getByTestId("confirm-cancel"));

    await waitFor(async () => {
      await expect(screen.queryByTestId("confirm-popup")).not.toBeInTheDocument();
    });
    await expect(trigger).not.toHaveAttribute("data-popup-open");
  },
};

/** Pressing the backdrop dismisses the dialog — the other pointer path. */
export const DismissOnBackdropPress: Story = {
  args: { defaultOpen: true },
  play: async () => {
    const backdrop = await screen.findByTestId("confirm-backdrop");

    await userEvent.click(backdrop);

    await waitFor(async () => {
      await expect(screen.queryByTestId("confirm-popup")).not.toBeInTheDocument();
    });
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes dialog.test.tsx's class-set assertions and still paints
 * nothing. These assertions read computed styles instead of class names.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { defaultOpen: true },
  play: async () => {
    const backdrop = await screen.findByTestId("confirm-backdrop");
    const popup = await screen.findByTestId("confirm-popup");

    // `fixed inset-0 bg-foreground/50` on the backdrop, against the unstyled
    // defaults of `static` and a transparent background.
    await expect(getComputedStyle(backdrop).position).toBe("fixed");
    await expect(getComputedStyle(backdrop).backgroundColor).not.toBe("rgba(0, 0, 0, 0)");

    // `fixed` centring, `bg-popover`, `border border-border`, `p-6` and
    // `rounded-lg` on the popup.
    const popupStyle = getComputedStyle(popup);
    await expect(popupStyle.position).toBe("fixed");
    await expect(popupStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(popupStyle.borderTopWidth).not.toBe("0px");
    await expect(popupStyle.borderTopStyle).toBe("solid");
    await expect(popupStyle.paddingTop).not.toBe("0px");
    await expect(popupStyle.borderTopLeftRadius).not.toBe("0px");

    // `text-lg font-semibold` on the title, against the <h2> defaults the reset
    // flattens to the body size.
    const titleStyle = getComputedStyle(await screen.findByTestId("confirm-title"));
    await expect(titleStyle.fontSize).not.toBe("16px");
    await expect(Number(titleStyle.fontWeight)).toBeGreaterThan(400);
  },
};
