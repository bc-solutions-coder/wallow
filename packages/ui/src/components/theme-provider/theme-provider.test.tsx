import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import {
  THEME_STORAGE_KEY,
  ThemeProvider,
  ThemeScript,
  themeInitScript,
  useTheme,
  type ThemeMode,
} from "./theme-provider";

/*
 * Wallow-lrlm.1.2 — theme activation in the REAL browser. The `browser` vitest
 * project, so `localStorage`, `matchMedia` and `document.documentElement` are
 * the genuine articles: nothing here is mocked (packages/ui mocks nothing), the
 * specs manipulate the real environment and restore it afterwards.
 *
 * The two claims that matter, and why they are asserted the way they are:
 *
 *   NO FLASH — the class has to land BEFORE first paint, which means before any
 *   React code runs at all. That is only provable by executing the actual script
 *   source: appending a `<script>` with `textContent` runs it synchronously on
 *   insertion, so an assertion taken on the very next line (no `await`, no
 *   `waitFor`) proves the stamping is synchronous. A spec that rendered the
 *   provider and then polled would pass for an implementation that stamps from
 *   `useEffect` — the exact defect the bead forbids.
 *
 *   NO HYDRATION MISMATCH — proved structurally rather than by watching the
 *   console: a mismatch IS the server and client markup disagreeing, so the SSR
 *   string and the client DOM are compared directly, and the class the script
 *   stamped is asserted to survive the provider's mount untouched.
 *
 * The precedence table itself lives in the sibling `theme-provider.test.ts`
 * (node project): `prefers-color-scheme` cannot be emulated from here, so the
 * cases below read the LIVE media query and derive what they expect from it.
 */

/** The colour scheme this browser actually reports, or `null` if it has none. */
function liveSystemMode(): ThemeMode | null {
  if (globalThis.matchMedia("(prefers-color-scheme: dark)").matches) {
    return "dark";
  }
  if (globalThis.matchMedia("(prefers-color-scheme: light)").matches) {
    return "light";
  }
  return null;
}

/**
 * Run the pre-paint script exactly as the document would. Appending a `<script>`
 * carrying `textContent` executes it SYNCHRONOUSLY, so callers can assert on the
 * next line.
 */
function runInitScript(defaultMode: ThemeMode): void {
  const element: HTMLScriptElement = document.createElement("script");
  element.textContent = themeInitScript(defaultMode);
  document.head.append(element);
  element.remove();
}

/** The theme classes only — app classes on `<html>` are none of our business. */
function themeClasses(): string[] {
  return [...document.documentElement.classList].filter(
    (name: string) => name === "light" || name === "dark",
  );
}

/** Renders the context value as data attributes so a spec can read it. */
function ThemeProbe(): ReactElement {
  const { mode, preference, setPreference } = useTheme();
  return (
    <button
      type="button"
      data-testid="probe"
      data-mode={mode}
      data-preference={preference}
      onClick={() => {
        setPreference("dark");
      }}
    >
      probe
    </button>
  );
}

let originalClassName = "";

beforeEach(() => {
  originalClassName = document.documentElement.className;
  document.documentElement.className = "";
  globalThis.localStorage.removeItem(THEME_STORAGE_KEY);
});

afterEach(() => {
  document.documentElement.className = originalClassName;
  globalThis.localStorage.removeItem(THEME_STORAGE_KEY);
});

describe("themeInitScript (executed)", () => {
  it("stamps the stored preference synchronously, before anything can paint", () => {
    globalThis.localStorage.setItem(THEME_STORAGE_KEY, "dark");

    runInitScript("light");

    // Deliberately no await: the class must already be there.
    expect(themeClasses()).toEqual(["dark"]);
  });

  it("lets the stored preference beat both the OS and the fork default", () => {
    globalThis.localStorage.setItem(THEME_STORAGE_KEY, "light");

    runInitScript("dark");

    expect(themeClasses()).toEqual(["light"]);
  });

  it("follows the OS when nothing is stored, and the fork default when the OS is silent", () => {
    const expected: ThemeMode = liveSystemMode() ?? "dark";

    runInitScript("dark");

    expect(themeClasses()).toEqual([expected]);
  });

  it("treats a stored 'system' as a request to follow the OS again", () => {
    globalThis.localStorage.setItem(THEME_STORAGE_KEY, "system");
    const expected: ThemeMode = liveSystemMode() ?? "light";

    runInitScript("light");

    expect(themeClasses()).toEqual([expected]);
  });

  it("replaces a stale mode class rather than stacking both", () => {
    // SSR emits the fork default on `<html>`; the script has to correct it, and
    // an element carrying BOTH classes resolves to whichever block comes last in
    // the stylesheet — a silent, order-dependent wrong theme.
    document.documentElement.classList.add("light");
    globalThis.localStorage.setItem(THEME_STORAGE_KEY, "dark");

    runInitScript("light");

    expect(themeClasses()).toEqual(["dark"]);
  });

  it("leaves classes it does not own alone", () => {
    document.documentElement.classList.add("h-full", "antialiased");
    globalThis.localStorage.setItem(THEME_STORAGE_KEY, "dark");

    runInitScript("light");

    expect(document.documentElement.classList.contains("h-full")).toBe(true);
    expect(document.documentElement.classList.contains("antialiased")).toBe(true);
  });
});

describe("ThemeScript", () => {
  it("renders the script source as the element's text, with nothing deferring it", async () => {
    const { container } = await render(<ThemeScript defaultMode="dark" />);

    const script = container.querySelector("script");
    expect(script).not.toBeNull();
    expect((script as HTMLScriptElement).textContent).toBe(themeInitScript("dark"));
    // `defer`/`async` would move it after parsing — i.e. after first paint,
    // which is the whole thing this component exists to beat.
    expect((script as HTMLScriptElement).hasAttribute("defer")).toBe(false);
    expect((script as HTMLScriptElement).hasAttribute("async")).toBe(false);
  });

  it("serializes to the same markup on the server", async () => {
    const { renderToString } = await import("react-dom/server");

    const html: string = renderToString(<ThemeScript defaultMode="dark" />);

    expect(html).toContain(themeInitScript("dark"));
  });
});

describe("ThemeProvider", () => {
  it("publishes the mode the script already stamped, not its own default", async () => {
    globalThis.localStorage.setItem(THEME_STORAGE_KEY, "dark");
    runInitScript("light");

    const { container } = await render(
      <ThemeProvider defaultMode="light">
        <ThemeProbe />
      </ThemeProvider>,
    );

    const probe = container.querySelector('[data-testid="probe"]');
    expect(probe?.getAttribute("data-mode")).toBe("dark");
    expect(probe?.getAttribute("data-preference")).toBe("dark");
  });

  it("reports 'system' as the preference when the visitor has chosen nothing", async () => {
    runInitScript("dark");

    const { container } = await render(
      <ThemeProvider defaultMode="dark">
        <ThemeProbe />
      </ThemeProvider>,
    );

    expect(container.querySelector('[data-testid="probe"]')?.getAttribute("data-preference")).toBe(
      "system",
    );
  });

  it("does not touch the class the script stamped when it mounts", async () => {
    // The no-flash guarantee's other half: recomputing on mount is what produces
    // the flash, so the class must be identical before and after — and stay
    // identical once effects have flushed.
    globalThis.localStorage.setItem(THEME_STORAGE_KEY, "dark");
    runInitScript("light");
    const stamped: string = document.documentElement.className;

    await render(
      <ThemeProvider defaultMode="light">
        <ThemeProbe />
      </ThemeProvider>,
    );

    await vi.waitFor(() => {
      expect(document.documentElement.className).toBe(stamped);
    });
    expect(themeClasses()).toEqual(["dark"]);
  });

  it("renders identical markup on the server and the client, so hydration cannot mismatch", async () => {
    const { renderToString } = await import("react-dom/server");
    const tree = (
      <ThemeProvider defaultMode="dark">
        <p data-testid="child">hello</p>
      </ThemeProvider>
    );

    const html: string = renderToString(tree);
    const { container } = await render(tree);

    expect(container.innerHTML).toBe(html);
  });

  it("renders no wrapper element of its own", async () => {
    // Anything it wrapped children in would be a new DOM node in every app's
    // body — and one more thing for SSR and the client to disagree about.
    const { container } = await render(
      <ThemeProvider defaultMode="dark">
        <p data-testid="child">hello</p>
      </ThemeProvider>,
    );

    expect(container.firstElementChild?.tagName).toBe("P");
    expect(container.childElementCount).toBe(1);
  });

  it("persists a new preference and re-stamps the document class", async () => {
    runInitScript("light");

    const { container } = await render(
      <ThemeProvider defaultMode="light">
        <ThemeProbe />
      </ThemeProvider>,
    );
    const probe = container.querySelector('[data-testid="probe"]') as HTMLElement;

    await userEvent.click(probe);

    expect(globalThis.localStorage.getItem(THEME_STORAGE_KEY)).toBe("dark");
    expect(themeClasses()).toEqual(["dark"]);
    expect(probe.getAttribute("data-mode")).toBe("dark");
    expect(probe.getAttribute("data-preference")).toBe("dark");
  });
});
