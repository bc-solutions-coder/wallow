import type { FailureReference } from "@bc-solutions-coder/api-errors";
import type { ReactElement } from "react";
import { toast, Toaster } from "sonner";

import { useTheme } from "../theme-provider/theme-provider";

/** The ids a toast may show, as `failureReference` answers them; the first present wins. */
export type { FailureReference } from "@bc-solutions-coder/api-errors";

/** Toast actions; inherited reference fields preserve the positional reference form. */
export interface FailureToastOptions extends FailureReference {
  readonly reference?: FailureReference | undefined;
  /** Sign-in takes precedence over a reference and its copy action. */
  readonly signInHref?: string | undefined;
}

/**
 * sonner injects its own unlayered stylesheet, which beats Tailwind's layered
 * utilities on the cascade. Every token here is therefore marked important so
 * the fork's palette wins; the keys are sonner's `toastOptions.classNames`.
 */
const TOAST_CLASSNAMES = {
  toast:
    "rounded-lg! border! border-border! bg-popover! text-popover-foreground! shadow-lg! font-sans!",
  title: "text-sm! font-semibold! text-foreground!",
  description: "text-sm! text-muted-foreground!",
  error: "border-destructive!",
  actionButton: "rounded-md! bg-primary! text-xs! font-medium! text-primary-foreground!",
  closeButton: "border-border! bg-popover! text-muted-foreground!",
} as const;

/** A denied clipboard has no second channel to report through; the toast stays. */
function ignoreClipboardDenial(): void {
  // Deliberately empty — see the doc comment.
}

/**
 * Raise a failure toast with optional sign-in or reference actions. Sign-in
 * takes precedence; either action keeps the toast until closed. A plain
 * reference object remains shorthand for `{ reference }`.
 */
export function toastFailure(message: string, options?: FailureToastOptions): void {
  const signInHref = options?.signInHref;
  if (signInHref !== undefined) {
    toast.error(message, {
      duration: Number.POSITIVE_INFINITY,
      action: {
        label: "Sign in",
        onClick: () => {
          globalThis.location.assign(signInHref);
        },
      },
    });
    return;
  }

  const reference = options?.reference ?? options;
  const id: string | undefined = reference?.traceId ?? reference?.requestId;

  if (id === undefined) {
    toast.error(message);
    return;
  }

  toast.error(message, {
    description: `Reference ${id}`,
    duration: Number.POSITIVE_INFINITY,
    action: {
      label: "Copy reference",
      onClick: (event) => {
        // sonner dismisses on action by default; copying is not done reading.
        event.preventDefault();
        // Absent on a plain-http origin, where the click simply does nothing.
        navigator.clipboard?.writeText(id).catch(ignoreClipboardDenial);
      },
    },
  });
}

/**
 * The one toaster an app mounts, under its `ThemeProvider`: bottom-right, a
 * close button on every toast, and sonner's theme fed from the resolved mode
 * so a toast never paints light over a dark page.
 */
export function FailureToaster(): ReactElement {
  const { mode } = useTheme();

  return (
    <Toaster
      theme={mode}
      position="bottom-right"
      closeButton
      toastOptions={{ classNames: TOAST_CLASSNAMES }}
    />
  );
}
