import { assertRouterStubApplied } from "@bc-solutions-coder/testing/router-stub";
import { Link } from "@tanstack/react-router";
import type { MouseEvent, ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useNavStore } from "./nav-store";
import { ShellFixture } from "./shell.fixtures";

/**
 * The nav's two DESKTOP states: the expanded rail (icon + label) and the
 * collapsed icon rail (icon only). Every case sets a viewport because Vitest
 * browser mode defaults to 414x896 — a phone — so a desktop spec that does not
 * say so is a mobile spec.
 *
 * The collapsed rail must carry NO text node: "deliberate icon rail" is only
 * distinguishable from "label clipped by the rail" if the item renders no text
 * at all and takes its accessible name from `aria-label`.
 */

// Which route the stubbed router considers active.
const routerState = vi.hoisted(() => ({ activePath: "" }));

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  className?: string;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// The stub applies `activeProps.className` on the active route — TanStack's
// active-link styling, which the nav must supply in BOTH desktop modes.
vi.mock("@tanstack/react-router", () => ({
  Link: Object.assign(
    ({ to, children, className, activeProps, onClick, ...rest }: LinkStubProps) => (
      <a
        href={to}
        data-router-stub="true"
        className={[className, to === routerState.activePath ? activeProps?.className : undefined]
          .filter(Boolean)
          .join(" ")}
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

/** Every row the rail renders, with the label it must be reachable by. */
const navItems: ReadonlyArray<readonly [testid: string, label: string]> = [
  ["dashboard-nav-organizations", "Organizations"],
  ["dashboard-nav-apps", "Apps"],
  ["dashboard-nav-settings", "Settings"],
  ["dashboard-nav-inquiries", "Inquiries"],
  ["dashboard-logout-link", "Sign Out"],
];

/** Widths on either side of the `md` breakpoint the nav switches on. */
const DESKTOP_VIEWPORT = [1280, 800] as const;

beforeEach(async () => {
  routerState.activePath = "";
  useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
  await page.viewport(...DESKTOP_VIEWPORT);
});

describe("AppNav expanded rail (desktop)", () => {
  it("reports itself expanded", async () => {
    await render(<ShellFixture />);

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "true");
  });

  it("renders an icon for every nav item", async () => {
    await render(<ShellFixture />);

    for (const [testid] of navItems) {
      await expect.element(page.getByTestId(testid)).toBeInTheDocument();
      expect(page.getByTestId(testid).element().querySelector("svg")).not.toBeNull();
    }
  });

  it("renders the visible label alongside every icon", async () => {
    await render(<ShellFixture />);

    for (const [testid, label] of navItems) {
      await expect.element(page.getByTestId(testid)).toHaveTextContent(label);
    }
  });

  it("highlights the active route", async () => {
    routerState.activePath = "/dashboard/settings";

    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-settings")).toBeInTheDocument();

    // "Differs from an inactive sibling" rather than an exact Tailwind string
    // keeps this about the behaviour, not the palette.
    const active: string = page.getByTestId("dashboard-nav-settings").element().className;
    const inactive: string = page.getByTestId("dashboard-nav-apps").element().className;
    expect(active).not.toBe(inactive);
  });
});

describe("AppNav collapsed icon rail (desktop)", () => {
  beforeEach(() => {
    useNavStore.setState({ isNavCollapsed: true });
  });

  it("reports itself collapsed", async () => {
    await render(<ShellFixture />);

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "false");
  });

  it("renders an icon for every nav item", async () => {
    await render(<ShellFixture />);

    for (const [testid] of navItems) {
      await expect.element(page.getByTestId(testid)).toBeInTheDocument();
      expect(page.getByTestId(testid).element().querySelector("svg")).not.toBeNull();
    }
  });

  it("renders no text node for any nav item", async () => {
    await render(<ShellFixture />);

    // A collapsed rail that keeps full-size labels clips them into "Settin".
    for (const [testid] of navItems) {
      await expect.element(page.getByTestId(testid)).toBeInTheDocument();
      expect(page.getByTestId(testid).element().textContent?.trim()).toBe("");
    }
  });

  it("names every icon-only nav item with an aria-label", async () => {
    await render(<ShellFixture />);

    for (const [testid, label] of navItems) {
      await expect.element(page.getByTestId(testid)).toHaveAttribute("aria-label", label);
    }
  });

  it("keeps every nav destination reachable by its accessible name", async () => {
    await render(<ShellFixture />);

    await expect.element(page.getByRole("link", { name: "Organizations" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Apps" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Settings" })).toBeInTheDocument();
    await expect.element(page.getByRole("link", { name: "Inquiries" })).toBeInTheDocument();
    await expect.element(page.getByRole("button", { name: "Sign Out" })).toBeInTheDocument();
  });

  it("highlights the active route", async () => {
    routerState.activePath = "/dashboard/settings";

    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-settings")).toBeInTheDocument();

    const active: string = page.getByTestId("dashboard-nav-settings").element().className;
    const inactive: string = page.getByTestId("dashboard-nav-apps").element().className;
    expect(active).not.toBe(inactive);
  });
});

describe("AppNav desktop toggle", () => {
  it("drops the labels when the rail collapses and restores them when it expands", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-settings")).toHaveTextContent("Settings");

    useNavStore.getState().toggleNavCollapsed();

    await expect
      .element(page.getByTestId("dashboard-nav"))
      .toHaveAttribute("data-nav-open", "false");
    expect(page.getByTestId("dashboard-nav-settings").element().textContent?.trim()).toBe("");

    useNavStore.getState().toggleNavCollapsed();

    await expect.element(page.getByTestId("dashboard-nav-settings")).toHaveTextContent("Settings");
  });

  it("keeps the rail mounted in both states so the toggle's aria-controls target survives", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav")).toHaveAttribute("id", "dashboard-nav");

    useNavStore.getState().toggleNavCollapsed();

    await expect.element(page.getByTestId("dashboard-nav")).toHaveAttribute("id", "dashboard-nav");
  });
});
