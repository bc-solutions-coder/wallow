import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";
import { toast } from "sonner";
import { expect, screen, userEvent, waitFor } from "storybook/test";

import { Button } from "../button/button";
import { FailureToaster, toastFailure } from "./failure-toast";

/*
 * The sonner failure toast under the real Tailwind pipeline: the one place the
 * forced token classes can be seen beating sonner's injected stylesheet. Each
 * story raises toasts through `toastFailure` from a Demo of buttons, the way an
 * app's `onUnhandledFailure` callback would. sonner keeps its toasts in a
 * module singleton, so every play clears it first — and waits for the exit
 * animation, since a dismissed toast stays in the DOM until it has played.
 */

async function clearToasts(): Promise<void> {
  toast.dismiss();
  await waitFor(() => expect(document.querySelector("[data-sonner-toast]")).toBeNull());
}

function Demo(): ReactElement {
  return (
    <div className="flex min-h-64 flex-wrap gap-2 p-4">
      <FailureToaster />
      <Button
        onClick={() =>
          toastFailure("Unable to reach the server. Check your connection and try again.", {
            requestId: "0HN7F3K2Q9P4R:00000003",
          })
        }
      >
        Transport failure
      </Button>
      <Button
        onClick={() =>
          toastFailure("Something went wrong on our side. Please try again later.", {
            traceId: "4bf92f3577b34da6a3ce929d0e0e4736",
          })
        }
      >
        5xx with reference
      </Button>
      <Button onClick={() => toastFailure("You don't have permission to do that.")}>403</Button>
    </div>
  );
}

const meta = {
  title: "Components/FailureToast",
  component: Demo,
} satisfies Meta<typeof Demo>;

export default meta;

type Story = StoryObj<typeof meta>;

/** A 5xx: sentence, reference line, copy action, close button. */
export const WithReference: Story = {
  play: async () => {
    await clearToasts();
    await userEvent.click(screen.getByRole("button", { name: "5xx with reference" }));
    await waitFor(() => expect(screen.getByText(/Reference 4bf92f35/)).toBeVisible());
    await expect(screen.getByRole("button", { name: "Copy reference" })).toBeVisible();
  },
};

/** A 403 carries no reference, so neither the line nor the action appears. */
export const WithoutReference: Story = {
  play: async () => {
    await clearToasts();
    await userEvent.click(screen.getByRole("button", { name: "403" }));
    await waitFor(() => expect(screen.getByText(/permission/)).toBeVisible());
    await expect(screen.queryByRole("button", { name: "Copy reference" })).toBeNull();
  },
};

/** Two failures in a row stack; the newest sits closest to the corner. */
export const Stacked: Story = {
  play: async () => {
    await clearToasts();
    await userEvent.click(screen.getByRole("button", { name: "Transport failure" }));
    await userEvent.click(screen.getByRole("button", { name: "403" }));
    await waitFor(() => expect(screen.getByText(/permission/)).toBeVisible());
    await expect(screen.getByText(/Reference 0HN7F3K2Q9P4R/)).toBeVisible();
  },
};
