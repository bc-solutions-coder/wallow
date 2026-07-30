import { render } from "@bc-solutions-coder/testing/render";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";

import { THEME_STORAGE_KEY, ThemeProvider } from "../theme-provider";
import { THEME_PREFERENCE_CYCLE, ThemeToggle, type ThemeToggleProps } from "./theme-toggle";

/*
 * Wallow-lrlm.1.2 — ThemeToggle. Same spec shape as the catalog exemplar
 * (Wallow-m5aq.2.1): browser vitest project, nothing mocked, the recipe asserted
 * THROUGH the component, class assertions as an order-free set. The rendered
 * LOOK of each state belongs to `theme-toggle.stories.tsx`, which runs with the
 * real Tailwind pipeline; what is left here is what a story cannot express — the
 * data attributes, the accessible name, the cycle order, and the override rule.
 *
 * THREE STATES, NOT TWO — `light -> dark -> system -> light`. This is the choice
 * the bead asked to be made explicitly. A two-state `aria-pressed` toggle can
 * say "I want dark" but can never get back to "follow the OS", and "follow the
 * OS" is both the default and the only state that keeps honouring a visitor
 * who changes their system theme later. The control therefore carries NO
 * `aria-pressed` (a two-state attribute would have to lie about the third
 * state); the current state is announced through the accessible name and
 * exposed to tests and E2E as `data-theme-preference`.
 *
 * `userEvent.click` is safe here even though the `browser` project compiles no
 * Tailwind: the toggle renders a text label, so it has intrinsic size and
 * Playwright's actionability check passes (see packages/ui/CLAUDE.md).
 */

/**
 * Every utility the toggle must end up with at the default size — the Button
 * recipe's `secondary` box merged with this component's own additions. It is
 * pinned as a merged SET because the merge is the contract: `themeToggleRecipe`
 * narrows the button's `w-full` down to `w-auto`, and a naive string append
 * would leave both on the element and render a full-bleed control.
 */
const TOGGLE_CLASSES = [
  "inline-flex",
  "items-center",
  "justify-center",
  "rounded-md",
  "px-3",
  "py-2",
  "text-sm",
  "font-medium",
  "outline-none",
  "motion-safe:transition-colors",
  "focus-visible:ring-2",
  "focus-visible:ring-ring",
  "data-[disabled]:opacity-50",
  "bg-secondary",
  "text-secondary-foreground",
  "hover:bg-secondary/80",
  "gap-2",
  "whitespace-nowrap",
  "w-auto",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

async function renderToggle(props: ThemeToggleProps = {}): Promise<HTMLElement> {
  const { container } = await render(<ThemeToggle data-testid="theme-toggle" {...props} />);

  const toggle = container.querySelector<HTMLElement>('[data-testid="theme-toggle"]');
  expect(toggle, "theme-toggle").not.toBeNull();
  return toggle as HTMLElement;
}

let originalClassName = "";

beforeEach(() => {
  originalClassName = document.documentElement.className;
  globalThis.localStorage.removeItem(THEME_STORAGE_KEY);
});

afterEach(() => {
  document.documentElement.className = originalClassName;
  globalThis.localStorage.removeItem(THEME_STORAGE_KEY);
});

describe("ThemeToggle", () => {
  it("cycles light -> dark -> system, and only those three", () => {
    // The documented order, pinned so the component and every caller agree.
    expect([...THEME_PREFERENCE_CYCLE]).toEqual(["light", "dark", "system"]);
  });

  it("renders a native button carrying the composed recipe class set", async () => {
    const toggle = await renderToggle({ preference: "light", mode: "light" });

    expect(toggle.tagName).toBe("BUTTON");
    expect(toggle.getAttribute("type")).toBe("button");
    expect(classSet(toggle)).toEqual(TOGGLE_CLASSES.toSorted());
  });

  it("narrows the button's full width instead of stacking both utilities", async () => {
    const toggle = await renderToggle({ preference: "light", mode: "light" });

    expect(toggle.classList.contains("w-auto")).toBe(true);
    expect(toggle.classList.contains("w-full")).toBe(false);
  });

  it("exposes the preference and the mode it resolves to as separate attributes", async () => {
    // Two axes, because `system` is not a mode: E2E and styling both need to
    // know WHAT WAS CHOSEN and WHAT IT CURRENTLY MEANS, and on `system` those
    // differ.
    const toggle = await renderToggle({ preference: "system", mode: "dark" });

    expect(toggle.getAttribute("data-theme-preference")).toBe("system");
    expect(toggle.getAttribute("data-theme-mode")).toBe("dark");
  });

  it("names itself after the state a press will move to", async () => {
    const toggle = await renderToggle({ preference: "light", mode: "light" });

    expect(toggle.getAttribute("aria-label")).toBe("Switch to dark theme");
  });

  it("shows the current state as its visible label", async () => {
    const toggle = await renderToggle({ preference: "system", mode: "dark" });

    expect(toggle.textContent).toBe("System");
  });

  it("carries no aria-pressed, because there are three states and not two", async () => {
    const toggle = await renderToggle({ preference: "dark", mode: "dark" });

    expect(toggle.hasAttribute("aria-pressed")).toBe(false);
  });

  it("reports the next preference in the cycle on each press", async () => {
    const onPreferenceChange = vi.fn();
    const toggle = await renderToggle({
      preference: "light",
      mode: "light",
      onPreferenceChange,
    });

    await userEvent.click(toggle);

    expect(onPreferenceChange).toHaveBeenCalledTimes(1);
    expect(onPreferenceChange.mock.calls[0]?.[0]).toBe("dark");
  });

  it("wraps from system back to light", async () => {
    const onPreferenceChange = vi.fn();
    const toggle = await renderToggle({
      preference: "system",
      mode: "light",
      onPreferenceChange,
    });

    await userEvent.click(toggle);

    expect(onPreferenceChange.mock.calls[0]?.[0]).toBe("light");
  });

  it("leaves a controlled toggle for its owner to update", async () => {
    // `preference` without a state update must NOT move: the component may not
    // keep private state behind the caller's back.
    const onPreferenceChange = vi.fn();
    const toggle = await renderToggle({
      preference: "dark",
      mode: "dark",
      onPreferenceChange,
    });

    await userEvent.click(toggle);

    expect(onPreferenceChange.mock.calls[0]?.[0]).toBe("system");
    expect(toggle.getAttribute("data-theme-preference")).toBe("dark");
  });

  it("advances from the keyboard with both Space and Enter", async () => {
    const onPreferenceChange = vi.fn();
    const toggle = await renderToggle({
      preference: "light",
      mode: "light",
      onPreferenceChange,
    });

    toggle.focus();
    await userEvent.keyboard(" ");
    await userEvent.keyboard("{Enter}");

    expect(onPreferenceChange).toHaveBeenCalledTimes(2);
    expect(onPreferenceChange.mock.calls[1]?.[0]).toBe("dark");
  });

  it("refuses to move while disabled", async () => {
    const onPreferenceChange = vi.fn();
    const toggle = await renderToggle({
      preference: "light",
      mode: "light",
      disabled: true,
      onPreferenceChange,
    });

    expect(toggle.hasAttribute("data-disabled")).toBe(true);

    await userEvent.click(toggle, { force: true });

    expect(onPreferenceChange).not.toHaveBeenCalled();
  });

  it("lets a caller className override a recipe utility", async () => {
    // The cn()/tailwind-merge proof: the conflicting utility is REMOVED rather
    // than appended after, and everything the caller never mentioned survives.
    const toggle = await renderToggle({ preference: "light", mode: "light", className: "px-6" });

    expect(toggle.classList.contains("px-6")).toBe(true);
    expect(toggle.classList.contains("px-3")).toBe(false);
    expect(toggle.classList.contains("py-2")).toBe(true);
    expect(toggle.classList.contains("bg-secondary")).toBe(true);
  });

  it("reads the surrounding ThemeProvider when no preference is supplied", async () => {
    globalThis.localStorage.setItem(THEME_STORAGE_KEY, "dark");
    document.documentElement.classList.remove("light");
    document.documentElement.classList.add("dark");

    const { container } = await render(
      <ThemeProvider defaultMode="light">
        <ThemeToggle data-testid="theme-toggle" />
      </ThemeProvider>,
    );

    const toggle = container.querySelector<HTMLElement>('[data-testid="theme-toggle"]');
    expect(toggle?.getAttribute("data-theme-preference")).toBe("dark");
    expect(toggle?.getAttribute("data-theme-mode")).toBe("dark");
  });

  it("drives the provider when pressed without a controlled preference", async () => {
    globalThis.localStorage.setItem(THEME_STORAGE_KEY, "light");
    document.documentElement.classList.remove("dark");
    document.documentElement.classList.add("light");

    const { container } = await render(
      <ThemeProvider defaultMode="light">
        <ThemeToggle data-testid="theme-toggle" />
      </ThemeProvider>,
    );
    const toggle = container.querySelector<HTMLElement>('[data-testid="theme-toggle"]');

    await userEvent.click(toggle as HTMLElement);

    expect(globalThis.localStorage.getItem(THEME_STORAGE_KEY)).toBe("dark");
    expect(document.documentElement.classList.contains("dark")).toBe(true);
    expect(document.documentElement.classList.contains("light")).toBe(false);
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(
      <ThemeToggle data-testid="dashboard-theme-toggle" preference="light" mode="light" />,
    );

    expect(container.querySelector('[data-testid="dashboard-theme-toggle"]')).not.toBeNull();
  });
});
