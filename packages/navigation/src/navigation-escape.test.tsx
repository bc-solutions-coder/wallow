import {
  NAVIGATION_ESCAPE_MESSAGE,
  assertNoNavigationEscape,
  clearNavigationEscapes,
  navigationEscapes,
} from "@bc-solutions-coder/testing/navigation-escape";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

/**
 * The guard that turns a leaked navigation into an assertion instead of a casualty.
 *
 * Unguarded, a hand-off out of the test iframe kills the runner mid-file and the
 * error names whichever file was loading next — so this package reports a
 * neighbour's death rather than the leak. Every provocation below is deliberate:
 * the real trigger is a race, and a spec must never wait on one.
 *
 * `installNavigationEscapeGuard()` is not called here. The project's setup file
 * owns that call, and this spec is the proof it happened.
 */

/** The destination the runner has actually been observed leaking to. */
const ESCAPE_PATH = "/dashboard/organizations";

/** The escape shape the iframe sees: an anchor whose default nobody prevented. */
function EscapeHatch(props: { routerStub?: boolean } = {}) {
  return (
    <a
      data-testid="escape-hatch"
      href={ESCAPE_PATH}
      data-router-stub={props.routerStub ? "true" : undefined}
    >
      Organizations
    </a>
  );
}

afterEach(() => {
  clearNavigationEscapes();
});

describe("navigation escape guard", () => {
  it("raises nothing while a spec navigates nowhere", async () => {
    await render(<EscapeHatch />);
    await expect.element(page.getByTestId("escape-hatch")).toBeInTheDocument();

    expect(navigationEscapes()).toEqual([]);
    expect(() => {
      assertNoNavigationEscape();
    }).not.toThrow();
  });

  it("keeps the runner in place when an unprevented anchor is clicked", async () => {
    await render(<EscapeHatch />);

    await userEvent.click(page.getByTestId("escape-hatch"));

    await vi.waitFor(() => {
      expect(navigationEscapes()).toHaveLength(1);
    });
    expect(navigationEscapes()[0]?.url).toContain(ESCAPE_PATH);
    expect(globalThis.location.pathname).not.toBe(ESCAPE_PATH);
  });

  it("names the escaped destination in the failure it raises", async () => {
    await render(<EscapeHatch />);
    await userEvent.click(page.getByTestId("escape-hatch"));
    await vi.waitFor(() => {
      expect(navigationEscapes()).toHaveLength(1);
    });

    expect(() => {
      assertNoNavigationEscape();
    }).toThrowError(new RegExp(`${NAVIGATION_ESCAPE_MESSAGE}[\\s\\S]*${ESCAPE_PATH}`, "u"));
  });

  it("captures forensics naming the element that initiated a click escape", async () => {
    await render(<EscapeHatch />);

    await userEvent.click(page.getByTestId("escape-hatch"));

    await vi.waitFor(() => {
      expect(navigationEscapes()).toHaveLength(1);
    });
    const forensics = navigationEscapes()[0]?.forensics;
    expect(forensics?.userInitiated).toBe(true);
    expect(forensics?.navigationType).toBe("push");
    expect(forensics?.sourceElement).toEqual({
      tag: "A",
      testId: "escape-hatch",
      routerStub: false,
    });
  });

  it("flags an initiating element that carries the router-stub marker", async () => {
    await render(<EscapeHatch routerStub />);

    await userEvent.click(page.getByTestId("escape-hatch"));

    await vi.waitFor(() => {
      expect(navigationEscapes()).toHaveLength(1);
    });
    expect(navigationEscapes()[0]?.forensics.sourceElement?.routerStub).toBe(true);
  });

  it("captures the rail's open state at veto time", async () => {
    await render(
      <div data-nav-open="false">
        <EscapeHatch />
      </div>,
    );

    await userEvent.click(page.getByTestId("escape-hatch"));

    await vi.waitFor(() => {
      expect(navigationEscapes()).toHaveLength(1);
    });
    expect(navigationEscapes()[0]?.forensics.navOpen).toBe("false");
  });

  it("renders the forensics into the failure it raises", async () => {
    await render(<EscapeHatch />);
    await userEvent.click(page.getByTestId("escape-hatch"));
    await vi.waitFor(() => {
      expect(navigationEscapes()).toHaveLength(1);
    });

    expect(() => {
      assertNoNavigationEscape();
    }).toThrowError(/initiated by <a data-testid="escape-hatch">[\s\S]*user gesture: yes/u);
  });

  it("catches a programmatic hand-off no router stub could intercept", async () => {
    await render(<EscapeHatch />);

    globalThis.location.assign(ESCAPE_PATH);

    await vi.waitFor(() => {
      expect(navigationEscapes()).toHaveLength(1);
    });
    expect(navigationEscapes()[0]?.url).toContain(ESCAPE_PATH);
  });
});
