import {
  computedColor,
  contrastRatio,
  effectiveBackground,
  isTransparent,
  over,
  textContrast,
  type Rgba,
} from "@bc-solutions-coder/testing/contrast";
import type { MouseEvent, ReactNode } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { DashboardNav } from "./DashboardNav";
import { useUiStore } from "../stores/ui-store";

/**
 * What the three CATALOG components inside the dashboard rail actually paint.
 *
 * `ThemeToggle`, `NavigationMenu.Link` and `ErrorBanner` render inside a rail
 * that is dark in BOTH modes, and each defaults to the PAGE palette. Measuring
 * is the point — a class string cannot see what `twMerge` produced. The mode is
 * stamped on `document.documentElement`, since `@theme` declares each token on
 * `:root` alone and a wrapper element measures light mode twice.
 */

const routerState = vi.hoisted(() => ({ activePath: "" }));

type LinkStubProps = {
  to: string;
  children?: ReactNode;
  className?: string;
  activeProps?: { className?: string };
  onClick?: (event: MouseEvent<HTMLAnchorElement>) => void;
} & Record<string, unknown>;

// `activeProps.className` reaches the DOM only through the active route.
vi.mock("@tanstack/react-router", () => ({
  Link: ({ to, children, className, activeProps, onClick, ...rest }: LinkStubProps) => (
    <a
      href={to}
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
}));

const logoutMock = vi.hoisted(() => vi.fn());

// Rejecting `logout()` is the only way to render the in-rail ErrorBanner.
vi.mock("@bc-solutions-coder/sdk", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk")>();
  return { ...actual, logout: logoutMock };
});

const DESKTOP_VIEWPORT = [1280, 800] as const;

/** WCAG 2.1 AA for body-sized text — every subject here is `text-sm` or larger. */
const AA_TEXT = 4.5;

/** WCAG 2.1 AA for a non-text boundary: the banner's edge against the rail. */
const AA_NON_TEXT = 3;

const MODES: readonly string[] = ["light", "dark"];

/**
 * The nav, a parking target for the mouse, and one probe per token needing a
 * REFERENCE colour, so nothing here hard-codes a hex. The parking target is
 * pinned above everything because Playwright retries to timeout on a covered
 * element.
 */
function NavUnderTest() {
  return (
    <div>
      <div data-testid="dashboard-nav-park" className="fixed top-0 right-0 z-50 size-8" />
      <div data-testid="probe-secondary" className="bg-secondary size-4" />
      <div data-testid="probe-sidebar" className="bg-sidebar size-4" />
      <div data-testid="probe-sidebar-accent" className="bg-sidebar-accent size-4" />
      <DashboardNav />
    </div>
  );
}

/** Select a mode the only way the token layer honours: on the document element. */
function applyMode(mode: string): void {
  document.documentElement.classList.toggle("dark", mode === "dark");
  document.documentElement.classList.toggle("light", mode === "light");
}

function probe(testId: string): Rgba {
  return computedColor(page.getByTestId(testId).element(), "background-color");
}

/**
 * Fail loudly when the theme is absent: a theme-less page paints every token
 * `rgba(0, 0, 0, 0)`, so "the toggle is not `bg-secondary`" passes because
 * NOTHING is anything.
 */
function expectThemed(color: Rgba, label: string): Rgba {
  expect(isTransparent(color), `${label}: paints nothing — is the fork theme loaded?`).toBe(false);
  return color;
}

beforeEach(async () => {
  routerState.activePath = "";
  logoutMock.mockReset();
  useUiStore.setState({ isNavCollapsed: false, isMobileNavOpen: false });
  await page.viewport(...DESKTOP_VIEWPORT);
});

// The document is shared by every case, so a stamped mode has to be handed back
// or the last `dark` case silently repaints whatever runs next.
afterEach(() => {
  document.documentElement.classList.remove("dark", "light");
});

describe("the mode axis itself", () => {
  it("repaints the tokens, so a dark-mode case is not light mode measured twice", async () => {
    // The tripwire for every `describe.each(MODES)` below: a mode selected on a
    // wrapper `<div>` measures the light palette under both labels and passes.
    applyMode("light");
    await render(<NavUnderTest />);
    const lightRail: Rgba = expectThemed(probe("probe-sidebar"), "bg-sidebar in light mode");
    const lightSecondary: Rgba = expectThemed(
      probe("probe-secondary"),
      "bg-secondary in light mode",
    );

    applyMode("dark");
    const darkRail: Rgba = expectThemed(probe("probe-sidebar"), "bg-sidebar in dark mode");
    const darkSecondary: Rgba = expectThemed(probe("probe-secondary"), "bg-secondary in dark mode");

    expect(darkRail, "bg-sidebar paints identically in both modes").not.toEqual(lightRail);
    expect(darkSecondary, "bg-secondary paints identically in both modes").not.toEqual(
      lightSecondary,
    );
  });
});

describe.each(MODES)("the theme toggle on the rail — %s mode", (mode: string) => {
  beforeEach(() => {
    applyMode(mode);
  });

  it("does not paint the page's secondary surface", async () => {
    await render(<NavUnderTest />);

    const toggle: Element = page.getByTestId("theme-toggle").element();
    const secondary: Rgba = expectThemed(probe("probe-secondary"), "bg-secondary");
    const painted: Rgba = expectThemed(
      effectiveBackground(toggle),
      "the toggle's rendered surface",
    );

    // `variant="secondary"` is a page-palette chip, which in light mode is
    // L 0.92 sitting on an L 0.22 rail.
    expect(painted, "the toggle still paints the page's secondary surface").not.toEqual(secondary);
  });

  it("paints a surface from the sidebar family", async () => {
    await render(<NavUnderTest />);

    const toggle: Element = page.getByTestId("theme-toggle").element();
    const painted: Rgba = expectThemed(
      effectiveBackground(toggle),
      "the toggle's rendered surface",
    );

    // Either token is legitimate: a chip takes `sidebar-accent`, a bordered or
    // ghost treatment lets the rail's own `sidebar` show through.
    expect(
      [expectThemed(probe("probe-sidebar"), "bg-sidebar"), probe("probe-sidebar-accent")],
      "the toggle's surface belongs to neither sidebar token",
    ).toContainEqual(painted);
  });

  it("keeps its label legible on whatever it paints", async () => {
    await render(<NavUnderTest />);
    const toggle: Element = page.getByTestId("theme-toggle").element();

    expectThemed(computedColor(toggle, "color"), "the toggle's label");

    const ratio: number = textContrast(toggle);

    expect(ratio, `the toggle's label measured ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(
      AA_TEXT,
    );
  });
});

/**
 * The theme's PAGE-surface colour tokens. None may reach a row on the rail — not
 * at rest, not behind a `hover:`, not behind a `data-[active]:`. `destructive`
 * is absent on purpose: the sign-out banner is an error wherever it renders, so
 * it may keep naming that token, and its legibility is measured above.
 */
const PAGE_SURFACE_TOKENS: ReadonlySet<string> = new Set([
  "background",
  "foreground",
  "card",
  "card-foreground",
  "popover",
  "popover-foreground",
  "primary",
  "primary-foreground",
  "secondary",
  "secondary-foreground",
  "muted",
  "muted-foreground",
  "accent",
  "accent-foreground",
]);

/** The utility prefixes that name a colour. `text-sm` is excluded by its TOKEN. */
const COLOUR_PREFIXES: readonly string[] = ["bg", "text", "border", "ring", "outline", "fill"];

/** Every class in `classes` that paints from the page palette. */
function pageSurfaceColorUtilities(classes: readonly string[]): readonly string[] {
  return classes.filter((cls: string): boolean => {
    // Strip every variant prefix and judge the utility underneath: a page colour
    // behind `data-[active]:` is still a page colour, and that is precisely where
    // the link recipe's inert pair hides.
    const utility: string = cls.split(":").at(-1) ?? "";

    return COLOUR_PREFIXES.some((prefix: string): boolean => {
      if (!utility.startsWith(`${prefix}-`)) {
        return false;
      }
      const token: string = utility.slice(prefix.length + 1).split("/")[0] ?? "";
      return PAGE_SURFACE_TOKENS.has(token);
    });
  });
}

/** The four destination rows, which share one class string through `itemClass`. */
const NAV_ROWS: readonly string[] = [
  "dashboard-nav-organizations",
  "dashboard-nav-apps",
  "dashboard-nav-settings",
  "dashboard-nav-inquiries",
];

describe("the nav rows' merged class attribute", () => {
  beforeEach(() => {
    applyMode("light");
  });

  /*
   * A class string rather than a colour, because an inert class paints nothing
   * to measure. `twMerge` drops a recipe class only when the caller conflicts
   * AT THE SAME VARIANT, so `data-[active]:bg-accent` rides along on all four
   * rows: Base UI sets `data-active` only from its own `active` prop, which the
   * app never passes (TanStack's `activeProps` is a className merge). It is a
   * light block latent on a dark rail, waiting for a state that reveals it.
   */
  it("flags a page colour behind any variant prefix", () => {
    // A detector that matched nothing would make the case below pass on any
    // markup at all. `text-sm` and `text-sidebar-foreground` are the traps.
    expect(
      pageSurfaceColorUtilities([
        "text-foreground",
        "hover:bg-accent",
        "data-[active]:text-accent-foreground",
        "text-sm",
        "rounded-md",
        "text-sidebar-foreground",
        "hover:bg-sidebar-accent",
      ]),
    ).toEqual(["text-foreground", "hover:bg-accent", "data-[active]:text-accent-foreground"]);
  });

  it("carries no page-surface colour on any row", async () => {
    await render(<NavUnderTest />);
    await expect.element(page.getByTestId("dashboard-nav-apps")).toBeInTheDocument();

    for (const testId of NAV_ROWS) {
      const row: Element = page.getByTestId(testId).element();

      expect(
        pageSurfaceColorUtilities([...row.classList]),
        `${testId} carries page-surface colour the consumer must out-merge by name`,
      ).toEqual([]);
    }
  });
});

describe.each(MODES)("the sign-out error banner on the rail — %s mode", (mode: string) => {
  beforeEach(() => {
    applyMode(mode);
  });

  /** Render the rail and drive Sign Out into its failure state. */
  async function failSignOut(): Promise<void> {
    logoutMock.mockRejectedValue(new Error("sign out refused"));

    await render(<NavUnderTest />);
    await userEvent.click(page.getByTestId("dashboard-logout-link").element());
    await expect.element(page.getByTestId("dashboard-logout-error")).toBeInTheDocument();
    await userEvent.hover(page.getByTestId("dashboard-nav-park").element());
  }

  it("keeps its message legible against the rail", async () => {
    await failSignOut();

    const banner: Element = page.getByTestId("dashboard-logout-error").element();
    const message: Element | null = banner.querySelector("p");

    expect(message, "the banner rendered no message paragraph").not.toBeNull();
    expectThemed(
      computedColor(page.getByTestId("dashboard-nav").element(), "background-color"),
      "the rail",
    );

    // `textContrast` composites both sides, so a `/10` fill is measured as what
    // the eye sees over the rail rather than as its authored colour.
    const ratio: number = textContrast(message as Element);

    expect(ratio, `the banner's message measured ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(
      AA_TEXT,
    );
  });

  it("is visually delineated from the rail it sits on", async () => {
    await failSignOut();

    const banner: Element = page.getByTestId("dashboard-logout-error").element();
    const rail: Rgba = expectThemed(
      computedColor(page.getByTestId("dashboard-nav").element(), "background-color"),
      "the rail",
    );

    // A banner may announce itself with a fill OR an edge, but with one of them:
    // a 10% tint and a border both within 3:1 of the rail is a message the
    // reader's eye never lands on.
    const fill: number = contrastRatio(effectiveBackground(banner), rail);
    const edge: number = contrastRatio(over(computedColor(banner, "border-top-color"), rail), rail);

    expect(
      Math.max(fill, edge),
      `the banner's fill measured ${fill.toFixed(2)}:1 and its edge ${edge.toFixed(2)}:1`,
    ).toBeGreaterThanOrEqual(AA_NON_TEXT);
  });
});
