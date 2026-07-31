import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { expectHeadingScale, HeadingScaleProbes } from "../../../.storybook/heading-scale";
import { AlertDialog } from "./alert-dialog";

/*
 * Wallow-m5aq.3.2 — Alert Dialog stories. `@storybook/addon-vitest` turns every
 * export below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec while
 * alert-dialog.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Three things belong HERE rather than in alert-dialog.test.tsx:
 *
 *   - POINTER interaction inside the open popup. Base UI always renders a fixed,
 *     full-window pointer blocker inside the portal, and `vitest/browser`'s
 *     `userEvent` drives real Playwright input, which hit-tests the click point
 *     and therefore hits the blocker instead of the popup. `userEvent` here is
 *     `@testing-library/user-event` (bundled by `storybook/test`), which
 *     dispatches synthetic events straight at the element with no hit-testing.
 *   - The REALISTIC proof that a backdrop press does not dismiss an alert dialog
 *     (StaysOpenOnBackdropPress) — Base UI's outside-press listens on
 *     `pointerdown`, which the `element.click()` the browser project is limited
 *     to never fires, so only a story can press the backdrop for real.
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens) — this project compiles real Tailwind and the
 *     `browser` project does not.
 *
 * A rule inherited from the Dialog exemplar, and the reason no assertion below
 * reads `toBeVisible()` straight after an open: the popup recipe carries a 150ms
 * enter transition starting at `opacity-0`, and jest-dom scores a computed
 * opacity of "0" as not visible. Every post-open visibility assertion is wrapped
 * in `waitFor`. (A story that passes against the red phase's empty stub recipe
 * proves nothing here — the stub has no transition to race.)
 */

interface DeleteAlertProps {
  /** Opens the alert on first render, for the screenshot stories. */
  readonly defaultOpen?: boolean;
  /** Called with the alert's new open state. */
  readonly onOpenChange?: (open: boolean) => void;
  /** Called when the destructive action is confirmed. */
  readonly onConfirm?: () => void;
}

/**
 * A complete, realistic destructive confirmation — the story subject. Both footer
 * buttons are `AlertDialog.Close` parts, which is the whole shape of an alert
 * dialog: Base UI ships no Action or Cancel part, so the confirm is just the
 * `Close` that also carries the caller's `onClick`.
 */
function DeleteAlert({ defaultOpen, onOpenChange, onConfirm }: DeleteAlertProps): ReactElement {
  return (
    <AlertDialog.Root defaultOpen={defaultOpen} onOpenChange={onOpenChange}>
      <AlertDialog.Trigger data-testid="delete-trigger">Delete project</AlertDialog.Trigger>
      <AlertDialog.Portal>
        <AlertDialog.Backdrop data-testid="delete-backdrop" />
        <AlertDialog.Popup data-testid="delete-popup">
          <AlertDialog.Title data-testid="delete-title">Delete project</AlertDialog.Title>
          <AlertDialog.Description data-testid="delete-description">
            This permanently removes the project and everything in it. You cannot undo this.
          </AlertDialog.Description>
          <div className="mt-6 flex justify-end gap-2">
            <AlertDialog.Close data-testid="delete-cancel">Cancel</AlertDialog.Close>
            <AlertDialog.Close
              data-testid="delete-confirm"
              variant="destructive"
              onClick={onConfirm}
            >
              Delete project
            </AlertDialog.Close>
          </div>
        </AlertDialog.Popup>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}

const meta = {
  title: "Components/AlertDialog",
  component: DeleteAlert,
  args: {
    onOpenChange: fn(),
    onConfirm: fn(),
  },
} satisfies Meta<typeof DeleteAlert>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The closed trigger — the state a page shows most of the time. */
export const Default: Story = {};

/** The open alert: backdrop, centred popup, title, description and two actions. */
export const Open: Story = {
  args: { defaultOpen: true },
};

/**
 * The two `Close` variants side by side, which is the only styling decision an
 * alert dialog asks a consumer to make: the confirm carries the destructive
 * variant, the cancel keeps the default `secondary`. Both are built from the
 * Button component's own `buttonRecipe`, so they cannot drift away from a real
 * `<Button>` elsewhere on the page.
 */
export const DestructiveActions: Story = {
  args: { defaultOpen: true },
  play: async () => {
    const confirm = await screen.findByTestId("delete-confirm");
    const cancel = await screen.findByTestId("delete-cancel");

    // Two different backgrounds, both painted — the variant axis is live rather
    // than a type that resolves to nothing.
    const confirmBackground = getComputedStyle(confirm).backgroundColor;
    const cancelBackground = getComputedStyle(cancel).backgroundColor;
    await expect(confirmBackground).not.toBe("rgba(0, 0, 0, 0)");
    await expect(cancelBackground).not.toBe("rgba(0, 0, 0, 0)");
    await expect(confirmBackground).not.toBe(cancelBackground);

    // `w-auto` beat the Button component's `w-full`, so the two sit in a row
    // rather than stacking full width.
    await expect(confirm.getBoundingClientRect().width).toBeLessThan(
      (confirm.parentElement as HTMLElement).getBoundingClientRect().width,
    );
  },
};

/**
 * The controlled shape: the open state lives in the caller's `useState`, and the
 * alert reports every change back through `onOpenChange`. This is the story a
 * consumer copies when the alert has to open from somewhere other than its own
 * trigger — a row action in a table, say.
 */
export const Controlled: Story = {
  render: function ControlledAlert(args) {
    const [open, setOpen] = useState(false);

    return (
      <div className="flex flex-col gap-2">
        <button
          type="button"
          data-testid="controlled-external"
          className="text-sm text-foreground"
          onClick={() => setOpen(true)}
        >
          Delete from outside the alert
        </button>
        <span data-testid="controlled-state">{open ? "open" : "closed"}</span>
        <AlertDialog.Root
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            args.onOpenChange?.(next);
          }}
        >
          <AlertDialog.Portal>
            <AlertDialog.Backdrop data-testid="controlled-backdrop" />
            <AlertDialog.Popup data-testid="controlled-popup">
              <AlertDialog.Title>Delete project</AlertDialog.Title>
              <AlertDialog.Description>The caller owns the open state.</AlertDialog.Description>
              <div className="mt-6 flex justify-end gap-2">
                <AlertDialog.Close data-testid="controlled-cancel">Cancel</AlertDialog.Close>
              </div>
            </AlertDialog.Popup>
          </AlertDialog.Portal>
        </AlertDialog.Root>
      </div>
    );
  },
  play: async ({ canvas }) => {
    await userEvent.click(canvas.getByTestId("controlled-external"));

    const popup = await screen.findByTestId("controlled-popup");
    await expect(popup).toHaveAttribute("data-open");
    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("open");

    await userEvent.click(screen.getByTestId("controlled-cancel"));

    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("closed");
    await waitFor(async () => {
      await expect(screen.queryByTestId("controlled-popup")).not.toBeInTheDocument();
    });
  },
};

/** The interaction half: opening from the trigger, then confirming the action. */
export const OpenAndConfirm: Story = {
  play: async ({ args, canvas }) => {
    const trigger = canvas.getByTestId("delete-trigger");

    await userEvent.click(trigger);

    // The popup is portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("delete-popup");
    // Wrapped because the popup recipe carries a 150ms enter transition starting
    // at opacity-0 — see the header.
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(popup).toHaveAttribute("role", "alertdialog");
    await expect(trigger).toHaveAttribute("data-popup-open");
    await expect(args.onOpenChange).toHaveBeenCalledWith(true, expect.anything());

    // Real Tailwind is loaded here, so the popup's `z-50` puts it above Base
    // UI's own pointer blocker and a genuine click lands. The `browser` project
    // cannot do this — see the header.
    await userEvent.click(screen.getByTestId("delete-confirm"));

    // The confirm is a `Close` that also runs the caller's handler: both happen.
    await expect(args.onConfirm).toHaveBeenCalled();
    await waitFor(async () => {
      await expect(screen.queryByTestId("delete-popup")).not.toBeInTheDocument();
    });
    await expect(trigger).not.toHaveAttribute("data-popup-open");
  },
};

/**
 * Pressing the backdrop does NOT dismiss an alert dialog — the behaviour that
 * makes this a separate component rather than a `Dialog` preset. Base UI's
 * outside-press path listens on `pointerdown`, so only a real pointer press can
 * exercise it, and only this project has one.
 */
export const StaysOpenOnBackdropPress: Story = {
  args: { defaultOpen: true },
  play: async ({ args }) => {
    const backdrop = await screen.findByTestId("delete-backdrop");

    await userEvent.click(backdrop);

    // Still mounted, still open, and `onOpenChange` was never told otherwise.
    await expect(await screen.findByTestId("delete-popup")).toHaveAttribute("data-open");
    await expect(args.onOpenChange).not.toHaveBeenCalledWith(false, expect.anything());

    // The only ways out are the actions and Escape.
    await userEvent.click(screen.getByTestId("delete-cancel"));
    await waitFor(async () => {
      await expect(screen.queryByTestId("delete-popup")).not.toBeInTheDocument();
    });
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes alert-dialog.test.tsx's class-set assertions and still
 * paints nothing. These assertions read computed styles instead of class names.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { defaultOpen: true },
  play: async () => {
    const backdrop = await screen.findByTestId("delete-backdrop");
    const popup = await screen.findByTestId("delete-popup");

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

    // `font-semibold` on the title, against the <h2> defaults the reset flattens
    // to the body weight. The SIZE is asserted by HeadingScale below, which
    // compares it against a probe rather than against a literal.
    const titleStyle = getComputedStyle(await screen.findByTestId("delete-title"));
    await expect(Number(titleStyle.fontWeight)).toBeGreaterThan(400);

    // The close recipe's inherited button padding actually paints, which is what
    // proves the cross-component `buttonRecipe` import reached the DOM.
    const confirmStyle = getComputedStyle(await screen.findByTestId("delete-confirm"));
    await expect(confirmStyle.paddingLeft).not.toBe("0px");
    await expect(confirmStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
  },
};

/**
 * The MEASURED pin on the catalog-wide heading standard (Wallow-io5f).
 *
 * `alertDialogTitleRecipe` hard-coded `text-lg` (18px) while `Text`'s
 * `subheading` step — the other spelling of a surface heading — sat at `text-xl`
 * (20px), so the same slot rendered at two sizes depending on which part a call
 * site reached for. This bead settles all four surface-title recipes on 20px.
 *
 * No app renders an AlertDialog today, which is exactly why this needs a test
 * rather than a note: unrendered is not never-rendered, and the first caller to
 * open one would otherwise inherit an 18px title with nothing to catch it. See
 * `.storybook/heading-scale.tsx` for why this is measured, and why here.
 */
export const HeadingScale: Story = {
  args: { defaultOpen: true },
  render: (args) => (
    <>
      <DeleteAlert {...args} />
      <HeadingScaleProbes />
    </>
  ),
  play: expectHeadingScale("delete-title"),
};
