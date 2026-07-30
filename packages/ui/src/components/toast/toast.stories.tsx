import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useEffect, useRef } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { Toast, useToastManager } from "./toast";

/*
 * Wallow-m5aq.3.11 — Toast stories. `@storybook/addon-vitest` turns every export
 * below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while toast.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * What belongs HERE rather than in toast.test.tsx:
 *
 *   - Anything that has to LOOK like a toast: the stacked corner viewport, a
 *     toast with an action, an error toast. Only this project compiles the
 *     utilities the recipes name.
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens) — the `browser` project compiles no Tailwind, so
 *     a recipe full of invented class names still passes toast.test.tsx.
 *
 * TWO RULES this file is careful about, both learned the hard way in this wave:
 *
 *   1. NEVER assert `toBeVisible()` on a freshly raised toast without wrapping it
 *      in `waitFor`. The root recipe carries a 150ms enter transition starting at
 *      `opacity-0`, and here — unlike the `browser` project — that transition is
 *      REAL, so the element exists a frame before it is visible.
 *   2. NEVER move the pointer over the viewport. Base UI pauses every
 *      auto-dismiss timer while the toast stack is hovered, so a stray hover
 *      would hang AutoDismiss. Every play function below clicks controls that sit
 *      OUTSIDE the viewport.
 */

/** Which toast the demo raises for itself on mount, for the screenshot stories. */
type InitialToast = "saved" | "stacked" | "undoable" | "failed";

interface ToastDemoProps {
  /** Raised on mount so a static story has something to show. */
  readonly initial?: InitialToast;
  /** The provider's default auto-dismiss, in milliseconds. */
  readonly timeout?: number;
  /** Called when a toast raised by this demo closes. */
  readonly onClose?: () => void;
}

/** Raises `initial` once the provider is mounted, so static stories show a toast. */
function InitialToastRaiser({ initial, onClose }: ToastDemoProps): null {
  const manager = useToastManager();

  useEffect(() => {
    if (initial === "saved") {
      manager.add({
        id: "saved",
        title: "Changes saved",
        description: "Your project settings are up to date.",
        onClose,
      });
    }

    if (initial === "stacked") {
      manager.add({ id: "first", title: "Upload started", description: "logo.svg" });
      manager.add({ id: "second", title: "Upload finished", description: "logo.svg — 42 KB" });
      manager.add({ id: "third", title: "Invite sent", description: "ada@example.com" });
    }

    if (initial === "undoable") {
      manager.add({
        id: "undoable",
        title: "Project deleted",
        description: "You can still undo this.",
        actionProps: { children: "Undo" },
        onClose,
      });
    }

    if (initial === "failed") {
      manager.add({
        id: "failed",
        type: "error",
        title: "Upload failed",
        description: "The file was larger than 10 MB.",
        onClose,
      });
    }
    // Raised once per mount; the manager is stable for the provider's lifetime.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return null;
}

/** The controls a story's play function drives. They sit OUTSIDE the viewport. */
function ToastControls({ onClose }: ToastDemoProps): ReactElement {
  const manager = useToastManager();
  // The upload promise settles only when the play function says so. A timer here
  // races the root's 150ms enter transition: on a slow runner the toast can reach
  // `toBeVisible` already re-typed to `success`, so `loading` is never observable.
  const finishUpload = useRef<((file: string) => void) | null>(null);

  return (
    <div className="flex flex-wrap gap-2">
      <button
        type="button"
        data-testid="toast-save"
        className="rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground"
        onClick={() => {
          manager.add({
            id: "saved",
            title: "Changes saved",
            description: "Your project settings are up to date.",
            onClose,
          });
        }}
      >
        Save changes
      </button>
      <button
        type="button"
        data-testid="toast-invite"
        className="rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground"
        onClick={() => {
          manager.add({ id: "invited", title: "Invite sent", description: "ada@example.com" });
        }}
      >
        Send invite
      </button>
      <button
        type="button"
        data-testid="toast-upload"
        className="rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground"
        onClick={() => {
          manager.promise(
            new Promise<string>((resolve) => {
              finishUpload.current = resolve;
            }),
            {
              loading: { title: "Uploading…" },
              success: (file) => ({ title: "Upload finished", description: file }),
              error: { title: "Upload failed" },
            },
          );
        }}
      >
        Upload file
      </button>
      <button
        type="button"
        data-testid="toast-upload-finish"
        className="rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground"
        onClick={() => {
          finishUpload.current?.("logo.svg");
        }}
      >
        Finish upload
      </button>
    </div>
  );
}

/**
 * A complete, realistic toast surface — the story subject. Stories drive the real
 * `Toast` namespace through this so the provider, the viewport and every part of
 * a toast are exercised together rather than one part at a time.
 */
function ToastDemo({ initial, timeout, onClose }: ToastDemoProps): ReactElement {
  return (
    <Toast.Provider timeout={timeout}>
      <ToastControls onClose={onClose} />
      <InitialToastRaiser initial={initial} onClose={onClose} />
      <Toast.Viewport data-testid="toast-viewport">
        <ToastEntries />
      </Toast.Viewport>
    </Toast.Provider>
  );
}

/** One `Toast.Root` per live toast, keyed by id so a play function can find it. */
function ToastEntries(): ReactElement {
  const { toasts } = useToastManager();

  return (
    <>
      {toasts.map((toast) => (
        <Toast.Root key={toast.id} toast={toast} data-testid={`toast-${toast.id}`}>
          <Toast.Content>
            <Toast.Title data-testid={`toast-title-${toast.id}`} />
            <Toast.Description data-testid={`toast-description-${toast.id}`} />
          </Toast.Content>
          <div className="flex justify-end gap-3">
            <Toast.Action data-testid={`toast-action-${toast.id}`} />
            <Toast.Close data-testid={`toast-close-${toast.id}`}>Dismiss</Toast.Close>
          </div>
        </Toast.Root>
      ))}
    </>
  );
}

const meta = {
  title: "Components/Toast",
  component: ToastDemo,
  args: {
    onClose: fn(),
  },
} satisfies Meta<typeof ToastDemo>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The empty surface: the controls that raise toasts, with nothing showing yet. */
export const Default: Story = {};

/** One toast in the corner — title, description and a dismiss button. */
export const WithToast: Story = {
  args: { initial: "saved" },
};

/** Three stacked toasts, newest first: what a busy page actually looks like. */
export const Stacked: Story = {
  args: { initial: "stacked" },
};

/** A toast whose action lets the user undo what just happened. */
export const WithAction: Story = {
  args: { initial: "undoable" },
};

/** An error toast: `data-type="error"` swaps the border to the destructive token. */
export const ErrorToast: Story = {
  args: { initial: "failed" },
};

/** Raising two toasts from the controls, then dismissing the newest. */
export const QueueAndDismiss: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByTestId("toast-save"));

    const saved = await screen.findByTestId("toast-saved");
    // The root recipe's 150ms enter transition starts at opacity-0, so the
    // element exists before it is visible — see rule 1 in the header.
    await waitFor(async () => {
      await expect(saved).toBeVisible();
    });
    await expect(screen.getByTestId("toast-title-saved")).toHaveTextContent("Changes saved");

    await userEvent.click(canvas.getByTestId("toast-invite"));

    const invited = await screen.findByTestId("toast-invited");
    await waitFor(async () => {
      await expect(invited).toBeVisible();
    });

    await userEvent.click(screen.getByTestId("toast-close-invited"));

    await waitFor(async () => {
      await expect(screen.queryByTestId("toast-invited")).not.toBeInTheDocument();
    });
    // Dismissing one toast leaves the rest of the stack alone.
    await expect(screen.getByTestId("toast-saved")).toBeInTheDocument();
    await expect(args.onClose).not.toHaveBeenCalled();
  },
};

/**
 * The timer path: a toast the caller never dismisses disappears on its own. The
 * provider's timeout is dialled down to keep the story fast — the real default
 * is Base UI's 5000ms.
 */
export const AutoDismiss: Story = {
  args: { timeout: 120 },
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByTestId("toast-save"));

    const saved = await screen.findByTestId("toast-saved");
    await waitFor(async () => {
      await expect(saved).toBeVisible();
    });

    // No dismiss click anywhere below: the timeout has to do this by itself.
    await waitFor(
      async () => {
        await expect(screen.queryByTestId("toast-saved")).not.toBeInTheDocument();
      },
      { timeout: 3000 },
    );
    await expect(args.onClose).toHaveBeenCalled();
  },
};

/**
 * A promise toast re-types ONE element as the work settles, rather than raising
 * a second toast — which is why `data-type` is a styling hook on the root.
 *
 * This is the one story that cannot query by `data-testid`: `promise()` takes no
 * id (`ToastManagerUpdateOptions` omits it), so Base UI generates one and the
 * only stable handle is the toast's own `role="dialog"`.
 */
export const PromiseToast: Story = {
  args: { timeout: 0 },
  play: async ({ canvas }) => {
    await userEvent.click(canvas.getByTestId("toast-upload"));

    const uploading = await screen.findByRole("dialog");
    await waitFor(async () => {
      await expect(uploading).toBeVisible();
    });
    await expect(uploading).toHaveAttribute("data-type", "loading");
    await expect(uploading).toHaveTextContent("Uploading…");

    // Only now does the promise settle — see `finishUpload` in ToastControls.
    await userEvent.click(canvas.getByTestId("toast-upload-finish"));

    await waitFor(async () => {
      await expect(uploading).toHaveAttribute("data-type", "success");
    });
    await expect(uploading).toHaveTextContent("Upload finished");
    await expect(uploading).toHaveTextContent("logo.svg");
    // One element the whole way through, not a second toast.
    await expect(screen.getAllByRole("dialog")).toHaveLength(1);
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes toast.test.tsx's class-set assertions and still paints
 * nothing. These assertions read computed styles instead of class names.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { initial: "failed", timeout: 0 },
  play: async () => {
    // `fixed right-4 bottom-4 z-50 flex w-80 flex-col gap-2` on the viewport,
    // against the unstyled defaults of `static`, `block` and `auto`.
    const viewportStyle = getComputedStyle(await screen.findByTestId("toast-viewport"));
    await expect(viewportStyle.position).toBe("fixed");
    await expect(viewportStyle.display).toBe("flex");
    await expect(viewportStyle.flexDirection).toBe("column");
    await expect(viewportStyle.bottom).not.toBe("auto");
    await expect(viewportStyle.right).not.toBe("auto");
    await expect(Number(viewportStyle.zIndex)).toBeGreaterThan(0);

    const root = await screen.findByTestId("toast-failed");
    await waitFor(async () => {
      await expect(root).toBeVisible();
    });

    // `bg-popover`, `border border-border`, `p-4`, `rounded-lg` and `shadow-lg`
    // on the root.
    const rootStyle = getComputedStyle(root);
    await expect(rootStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(rootStyle.borderTopWidth).not.toBe("0px");
    await expect(rootStyle.borderTopStyle).toBe("solid");
    await expect(rootStyle.paddingTop).not.toBe("0px");
    await expect(rootStyle.borderTopLeftRadius).not.toBe("0px");
    await expect(rootStyle.boxShadow).not.toBe("none");

    // `data-[type=error]:border-destructive` is the one state-driven utility a
    // caller cannot reach with a prop, so it is only provable by painting: an
    // error toast's border must differ from its own text colour.
    await expect(rootStyle.borderTopColor).not.toBe(rootStyle.color);

    // `font-semibold` on the title against `text-muted-foreground` on the
    // description: the two lines have to be distinguishable.
    const titleStyle = getComputedStyle(await screen.findByTestId("toast-title-failed"));
    const descriptionStyle = getComputedStyle(
      await screen.findByTestId("toast-description-failed"),
    );
    await expect(Number(titleStyle.fontWeight)).toBeGreaterThan(400);
    await expect(descriptionStyle.color).not.toBe(titleStyle.color);
  },
};
