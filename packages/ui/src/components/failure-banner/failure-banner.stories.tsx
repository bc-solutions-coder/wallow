import { ApiFailure, ClientErrorCode, ErrorCode } from "@bc-solutions-coder/api-errors";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, screen, userEvent } from "storybook/test";

import { FailureBanner } from "./failure-banner";

/*
 * One story per status rule, so a reviewer can see which affordances each kind
 * of failure earns: a 4xx is its sentence alone, a 401 adds the sign-in link,
 * a transport or 5xx failure adds the reference and copy action, and a retry
 * adds "Try again" to any of them.
 */

const meta = {
  title: "Components/FailureBanner",
  component: FailureBanner,
} satisfies Meta<typeof FailureBanner>;

export default meta;

type Story = StoryObj<typeof meta>;

/** A 4xx the API explained itself: the detail is the sentence, nothing else. */
export const ClientProblem: Story = {
  args: {
    error: new ApiFailure({
      status: 409,
      code: "Orders.Closed",
      title: "Conflict",
      detail: "The order is already closed.",
      traceId: "4bf92f3577b34da6a3ce929d0e0e4736",
    }),
  },
  play: async () => {
    await expect(screen.queryByText(/Reference/)).toBeNull();
    await expect(screen.queryByRole("link", { name: "Sign in" })).toBeNull();
  },
};

/** A 401: the shipped session copy plus a link back through the BFF login. */
export const Unauthenticated: Story = {
  args: {
    error: new ApiFailure({
      status: 401,
      code: ErrorCode.AUTH_UNAUTHENTICATED,
      title: "Unauthorized",
    }),
  },
  play: async () => {
    const signIn = screen.getByRole("link", { name: "Sign in" });
    await expect(signIn).toHaveAttribute("href", expect.stringMatching(/^\/bff\/login\?returnTo=/));
  },
};

/** A 5xx: fixed copy (never the detail), the trace id, and a copy action. */
export const ServerError: Story = {
  args: {
    error: new ApiFailure({
      status: 500,
      code: ErrorCode.SERVER_ERROR,
      title: "Internal Server Error",
      detail: "NullReferenceException at Wallow.Orders",
      traceId: "4bf92f3577b34da6a3ce929d0e0e4736",
    }),
    onRetry: fn(),
  },
  play: async ({ args }) => {
    await expect(screen.queryByText(/NullReferenceException/)).toBeNull();
    await expect(screen.getByText(/Reference 4bf92f35/)).toBeVisible();
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    await expect(args.onRetry).toHaveBeenCalledTimes(1);
  },
};

/** A transport failure carries the request id the BFF stamped on it. */
export const Transport: Story = {
  args: {
    error: new ApiFailure({
      status: 503,
      code: ClientErrorCode.TRANSPORT_NETWORK_ERROR,
      title: "Service Unavailable",
      requestId: "0HN7F3K2Q9P4R:00000003",
    }),
  },
  play: async () => {
    await expect(screen.getByText(/Reference 0HN7F3K2Q9P4R/)).toBeVisible();
  },
};

/** Children follow the sentence; the sidebar arm keeps them legible on the rail. */
export const OnTheSidebarSurface: Story = {
  args: {
    error: new ApiFailure({
      status: 403,
      code: ErrorCode.AUTH_FORBIDDEN,
      title: "Forbidden",
    }),
    surface: "sidebar",
    children: " Ask an administrator for access.",
  },
  render: (args) => (
    <div className="bg-sidebar p-6">
      <FailureBanner {...args} />
    </div>
  ),
};
