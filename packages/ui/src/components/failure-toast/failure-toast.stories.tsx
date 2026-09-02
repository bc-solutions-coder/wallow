import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";
import { expect, screen, userEvent, waitFor } from "storybook/test";

import { Button } from "../button/button";
import { FailureToaster, toastFailure } from "./failure-toast";

/* PROTOTYPE — wayfinder ticket #168. Visual check of the sonner failure toast. */

function Demo(): ReactElement {
  return (
    <div className="flex min-h-64 gap-2 p-4">
      <FailureToaster />
      <Button
        onClick={() =>
          toastFailure("Unable to reach the server. Check your connection and try again.")
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

const meta: Meta<typeof Demo> = { title: "Prototype/FailureToast", component: Demo };
export default meta;

type Story = StoryObj<typeof Demo>;

export const Raised: Story = {
  play: async () => {
    await userEvent.click(screen.getByRole("button", { name: "5xx with reference" }));
    await waitFor(() => expect(screen.getByText(/Reference 4bf92f35/)).toBeVisible());
  },
};

export const Stacked: Story = {
  play: async () => {
    await userEvent.click(screen.getByRole("button", { name: "Transport failure" }));
    await userEvent.click(screen.getByRole("button", { name: "403" }));
    await waitFor(() => expect(screen.getByText(/permission/)).toBeVisible());
  },
};
