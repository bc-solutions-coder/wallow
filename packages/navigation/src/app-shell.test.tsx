import type { MouseEvent, ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { ShellFixture } from "./shell.fixtures";

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// The shell composes `AppNav`, whose TanStack `Link`s require live router
// context, so stub `Link` to a plain anchor passing `to` -> `href` + testids.
// The default navigation is suppressed so no stray click moves the test iframe.
// `onClick` is pulled out of `rest` so a spread handler cannot land after — and
// thus replace — the one that calls `preventDefault`.
vi.mock("@tanstack/react-router", () => ({
  Link: ({ to, children, activeProps: _activeProps, onClick, ...rest }: LinkStubProps) => (
    <a
      href={to}
      onClick={(event: MouseEvent<HTMLAnchorElement>) => {
        event.preventDefault();
        onClick?.(event);
      }}
      {...rest}
    >
      {children}
    </a>
  ),
}));

/**
 * The shell's composition: a `{prefix}-shell` root, the nav's destinations, and
 * the main column holding whatever the app routed into it.
 *
 * `children` rather than an `Outlet` this package renders — an `Outlet` binds to
 * the app's own route tree, so taking it as children is what lets a fork mount
 * the shell anywhere.
 */
describe("AppShell", () => {
  // Vitest browser mode defaults to a 414x896 viewport — a phone, below the `md`
  // breakpoint at which the shell renders the nav rail rather than a mobile
  // drawer. These cases assert the desktop composition.
  beforeEach(async () => {
    await page.viewport(1280, 800);
  });

  it("renders a shell root carrying data-testid={prefix}-shell", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-shell")).toBeInTheDocument();
  });

  it("renders the nav's destinations", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-organizations")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-apps")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-settings")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-inquiries")).toBeInTheDocument();
  });

  it("renders the routed content it was handed", async () => {
    await render(
      <ShellFixture>
        <div data-testid="routed-content" />
      </ShellFixture>,
    );

    await expect.element(page.getByTestId("routed-content")).toBeInTheDocument();
  });

  it("puts the routed content in the main landmark, beside the rail rather than inside it", async () => {
    await render(
      <ShellFixture>
        <div data-testid="routed-content" />
      </ShellFixture>,
    );
    await expect.element(page.getByTestId("routed-content")).toBeInTheDocument();

    const content: Element = page.getByTestId("routed-content").element();
    expect(
      content.closest("main"),
      "the routed content must sit in the main landmark",
    ).not.toBeNull();
    expect(
      content.closest('[data-testid="dashboard-nav"]'),
      "the routed content must not be inside the rail",
    ).toBeNull();
  });
});
