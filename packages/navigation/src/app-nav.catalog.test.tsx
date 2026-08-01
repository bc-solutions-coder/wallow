import type { MouseEvent, ReactNode } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useNavStore } from "./nav-store";
import { ShellFixture } from "./shell.fixtures";

/**
 * The nav's LIST semantics, in all three modes.
 *
 * Four anchors dropped into a `<nav>` announce as four loose links, with no
 * count and no "3 of 4" position; a navigation menu announces one list of four
 * items. Structure is asserted by ROLE, accepting native `ul`/`li` or an
 * explicit one — what a screen reader is handed is the contract, not the
 * element chosen.
 */

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  className?: string;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// Stub TanStack `Link` to a plain anchor. `activeProps` is dropped rather than
// spread, since letting it fall into `...rest` puts an object on a DOM attribute.
vi.mock("@tanstack/react-router", () => ({
  Link: ({
    to,
    children,
    className,
    activeProps: _activeProps,
    onClick,
    ...rest
  }: LinkStubProps) => (
    <a
      href={to}
      className={className}
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

/** The four destinations the rail and the drawer both render. */
const destinationTestIds: readonly string[] = [
  "dashboard-nav-organizations",
  "dashboard-nav-apps",
  "dashboard-nav-settings",
  "dashboard-nav-inquiries",
];

const DESKTOP_VIEWPORT = [1280, 800] as const;
const MOBILE_VIEWPORT = [390, 844] as const;

/** The single element carrying `testId`; fails loudly when absent or duplicated. */
function byTestId(testId: string): HTMLElement {
  const elements = page.getByTestId(testId).elements();
  expect(elements, `expected exactly one [data-testid="${testId}"]`).toHaveLength(1);
  return elements[0] as HTMLElement;
}

/** The list item `testId` sits in — native `<li>` or an explicit role. */
function listItemFor(testId: string): HTMLElement | null {
  return byTestId(testId).closest<HTMLElement>('li, [role="listitem"]');
}

/** The list `testId` belongs to — native `<ul>`/`<ol>` or an explicit role. */
function listFor(testId: string): HTMLElement | null {
  return byTestId(testId).closest<HTMLElement>('ul, ol, [role="list"]');
}

/** Every destination is a listitem, and all four belong to the SAME list. */
async function expectOneNavigationList(): Promise<void> {
  for (const testId of destinationTestIds) {
    await expect.element(page.getByTestId(testId)).toBeInTheDocument();
  }

  const firstList: HTMLElement | null = listFor(destinationTestIds[0] as string);
  expect(firstList, "the nav destinations must be gathered into a list").not.toBeNull();

  for (const testId of destinationTestIds) {
    expect(listItemFor(testId), `${testId} must be a list item`).not.toBeNull();
    expect(listFor(testId), `${testId} must share the other destinations' list`).toBe(firstList);
  }

  // One item per destination, so the list announces "4 items" rather than
  // counting furniture beside them.
  const items = (firstList as HTMLElement).querySelectorAll(
    ':scope > li, :scope > [role="listitem"]',
  );
  expect(items).toHaveLength(destinationTestIds.length);
}

/** The navigation landmark the list must stay inside, in every mode. */
function expectListInNavigationLandmark(): void {
  const list: HTMLElement | null = listFor(destinationTestIds[0] as string);
  expect(list, "the nav destinations must be gathered into a list").not.toBeNull();
  expect(
    (list as HTMLElement).closest('nav, [role="navigation"]'),
    "the list must sit inside a navigation landmark",
  ).not.toBeNull();
}

beforeEach(() => {
  useNavStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
});

describe("AppNav expanded rail (catalog NavigationMenu)", () => {
  beforeEach(async () => {
    await page.viewport(...DESKTOP_VIEWPORT);
  });

  it("gathers every destination into one navigation list", async () => {
    await render(<ShellFixture />);

    await expectOneNavigationList();
  });

  it("keeps that list inside a navigation landmark", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-apps")).toBeInTheDocument();

    expectListInNavigationLandmark();
  });

  it("drops a gated destination from the list rather than emptying its item", async () => {
    await render(<ShellFixture isAdmin={false} />);
    await expect.element(page.getByTestId("dashboard-nav-apps")).toBeInTheDocument();

    // Gating removes a destination; the list must shrink with it, or a screen
    // reader is told there are four items and handed three.
    await expect.element(page.getByTestId("dashboard-nav-organizations")).not.toBeInTheDocument();

    const list: HTMLElement | null = listFor("dashboard-nav-apps");
    expect(list).not.toBeNull();
    const items = (list as HTMLElement).querySelectorAll(':scope > li, :scope > [role="listitem"]');
    expect(items).toHaveLength(destinationTestIds.length - 1);
  });
});

describe("AppNav collapsed icon rail (catalog NavigationMenu)", () => {
  beforeEach(async () => {
    await page.viewport(...DESKTOP_VIEWPORT);
    useNavStore.setState({ isNavCollapsed: true });
  });

  it("gathers every destination into one navigation list", async () => {
    await render(<ShellFixture />);

    await expectOneNavigationList();
  });

  it("keeps the list semantics while the labels are gone", async () => {
    await render(<ShellFixture />);
    await expectOneNavigationList();

    // The icon rail renders no text and takes its names from `aria-label`;
    // collapsing must not take the list structure down with the labels.
    for (const testId of destinationTestIds) {
      expect(byTestId(testId).textContent?.trim()).toBe("");
      expect(byTestId(testId).getAttribute("aria-label")).not.toBeNull();
    }
  });
});

describe("AppNav mobile drawer (catalog NavigationMenu)", () => {
  beforeEach(async () => {
    await page.viewport(...MOBILE_VIEWPORT);
    useNavStore.setState({ isMobileNavOpen: true });
  });

  it("gathers every destination into one navigation list", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();

    await expectOneNavigationList();
  });

  it("keeps that list inside a navigation landmark", async () => {
    await render(<ShellFixture />);
    await expect.element(page.getByTestId("dashboard-nav-drawer")).toBeInTheDocument();

    expectListInNavigationLandmark();
  });
});
