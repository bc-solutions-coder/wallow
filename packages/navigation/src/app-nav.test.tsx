import { assertRouterStubApplied } from "@bc-solutions-coder/testing/router-stub";
import { Link } from "@tanstack/react-router";
import type { MouseEvent, ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { fixtureDestinations, ShellFixture } from "./shell.fixtures";

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// The nav renders TanStack `Link`s, whose hooks throw outside a
// `RouterProvider`, so stub `Link` to a plain anchor that passes through `to`
// (as `href`) and any `data-testid`. The default navigation is suppressed so no
// stray click moves the test iframe, and `onClick` is pulled out of `rest` so a
// spread handler cannot land after — and thus replace — the suppression.
vi.mock("@tanstack/react-router", () => ({
  Link: Object.assign(
    ({ to, children, activeProps: _activeProps, onClick, ...rest }: LinkStubProps) => (
      <a
        href={to}
        data-router-stub="true"
        onClick={(event: MouseEvent<HTMLAnchorElement>) => {
          event.preventDefault();
          onClick?.(event);
        }}
        {...rest}
      >
        {children}
      </a>
    ),
    { wallowRouterStub: true },
  ),
}));

beforeEach(() => {
  assertRouterStubApplied(Link);
});

/**
 * Every destination in the manifest reaches the route it names, under a testid
 * derived from the shell's prefix and the destination's own `id`.
 *
 * Driven from the manifest rather than a copied table: a destination the loop
 * forgot is exactly the regression this covers.
 */
describe("AppNav destination links", () => {
  // Vitest browser mode defaults to a 414x896 viewport — a phone, below the `md`
  // breakpoint at which the nav renders a rail at all. These cases are about the
  // desktop rail's links, so they must say so.
  beforeEach(async () => {
    await page.viewport(1280, 800);
  });

  for (const destination of fixtureDestinations) {
    it(`renders dashboard-${destination.id} -> ${String(destination.to)}`, async () => {
      await render(<ShellFixture />);
      const link = page.getByTestId(`dashboard-${destination.id}`);
      await expect.element(link).toBeInTheDocument();
      await expect.element(link).toHaveAttribute("href", String(destination.to));
    });
  }

  it("derives every testid from the shell's prefix", async () => {
    await render(<ShellFixture testIdPrefix="console" />);

    await expect.element(page.getByTestId("console-nav-apps")).toBeInTheDocument();
    await expect.element(page.getByTestId("dashboard-nav-apps")).not.toBeInTheDocument();
  });
});
