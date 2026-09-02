/**
 * PROTOTYPE — wayfinder ticket #168 (map #163). Throwaway: answers "can `query`
 * stay UI-free and what is the opt-out contract", and shows what a sonner
 * failure toast looks like on the branding tokens. Not exported from the root
 * barrel on purpose; reach it via `@bc-solutions-coder/ui/failure-toast`.
 */
import type { ReactElement } from "react";
import { Toaster, toast } from "sonner";

import { useTheme } from "../theme-provider/theme-provider";

/** The slice of an API failure a toast cares about. */
export interface FailureReference {
  readonly traceId?: string;
  readonly requestId?: string;
}

/**
 * Raise one failure toast. `message` is the already-resolved failure message;
 * the reference (trace id, else request id) goes in the description and behind a
 * copy action so a user can hand it to support. `preventDefault` keeps the toast
 * open after the copy.
 */
export function toastFailure(message: string, reference?: FailureReference): void {
  const ref: string | undefined = reference?.traceId ?? reference?.requestId;
  toast.error(message, {
    description: ref === undefined ? undefined : `Reference ${ref}`,
    action:
      ref === undefined
        ? undefined
        : {
            label: "Copy reference",
            onClick: (event) => {
              event.preventDefault();
              void navigator.clipboard.writeText(ref);
            },
          },
  });
}

/*
 * sonner injects its stylesheet as an UNLAYERED <style>, which beats Tailwind
 * v4's layered utilities, so every override below carries `!` (research #171).
 * Colours come from the branding tokens the rest of the catalog paints with.
 */
const TOAST_CLASSNAMES = {
  toast: "!rounded-lg !border !border-border !bg-popover !text-popover-foreground !shadow-lg",
  title: "!text-sm !font-semibold !text-foreground",
  description: "!text-sm !text-muted-foreground",
  error: "!border-destructive",
  actionButton: "!bg-primary !text-primary-foreground !rounded-md !text-xs !font-medium",
  closeButton: "!border-border !bg-popover !text-muted-foreground",
} as const;

/**
 * The one toaster an app mounts, inside `<body>` and under `ThemeProvider`.
 * sonner has no class-based dark mode, so the active theme mode is passed as its
 * `theme` prop rather than letting it read `prefers-color-scheme` itself.
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
