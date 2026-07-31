import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";
import { useState } from "react";

import type { ThemePreference } from "../theme-provider";
import { expectScheme } from "../../../.storybook/scheme-assertions";
import { darkScheme, lightScheme } from "../../../.storybook/scheme-decorators";
import { ThemeToggle } from "./theme-toggle";

/*
 * Wallow-lrlm.1.2 — ThemeToggle stories. Each export becomes a Vitest test case
 * in the same headless Chromium the `browser` project uses, but with the real
 * Tailwind pipeline and the fork's real theme attached (.storybook/preview.css +
 * preview.tsx), so this is the only place the control's secondary-token colours
 * can actually be seen — in BOTH schemes.
 *
 * Every story renders the toggle CONTROLLED (`preference` + `mode` supplied), so
 * no story reaches for `localStorage` or a real `ThemeProvider`: the `mode` prop
 * says what the control should show, and the decorator independently puts the
 * page in that scheme.
 *
 * What a WRAPPER cannot do, and why the decorators do not use one
 * (Wallow-lrlm.6.4, fixed in Wallow-lrlm.11): repaint the `--color-*` tokens.
 * `renderThemeStyle` emits `:root` / `.dark` / `.light` blocks carrying the RAW
 * `--sidebar`-style variables, while `@theme` declares
 * `--color-sidebar: var(--sidebar, …)` on `:root` alone — and a `var()` inside a
 * custom property is substituted at computed-value time on the DECLARING element.
 * A `.dark` wrapper rebinds the raw variable for its descendants; the token was
 * already computed at `:root` from the light one, so every utility keeps painting
 * light. The shared `lightScheme`/`darkScheme` decorators therefore stamp the
 * class on `document.documentElement`, where both blocks meet, and remove it on
 * unmount — stories share one document, so the cleanup is what keeps a scheme
 * inside the story that asked for it. Each scheme-scoped story then MEASURES the
 * palette it paints through `expectScheme`, so a wrapper coming back, or a
 * decorator that stopped cleaning up, turns a story red.
 *
 * Callback spies come from `fn()` in `storybook/test` (never `vi.fn()`, which
 * the Interactions panel cannot display).
 */

const meta = {
  title: "Components/ThemeToggle",
  component: ThemeToggle,
  args: { onPreferenceChange: fn() },
} satisfies Meta<typeof ThemeToggle>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The visitor explicitly chose light. */
export const Light: Story = {
  args: { preference: "light", mode: "light" },
  decorators: [lightScheme],
  play: expectScheme("light"),
};

/** The visitor explicitly chose dark. */
export const Dark: Story = {
  args: { preference: "dark", mode: "dark" },
  decorators: [darkScheme],
  play: expectScheme("dark"),
};

/**
 * The default: no choice made, so the OS decides. The label reads `System` while
 * the control renders in whatever scheme that currently resolves to — here the
 * light one.
 */
export const SystemLight: Story = {
  args: { preference: "system", mode: "light" },
  decorators: [lightScheme],
  play: expectScheme("light"),
};

/** The same `system` preference, resolving the other way. */
export const SystemDark: Story = {
  args: { preference: "system", mode: "dark" },
  decorators: [darkScheme],
  play: expectScheme("dark"),
};

/** Disabled, so the fork can suppress the control without hiding it. */
export const Disabled: Story = {
  args: { preference: "light", mode: "light", disabled: true },
  decorators: [lightScheme],
  play: expectScheme("light"),
};

/**
 * The interaction half: three presses walk the whole cycle and come back where
 * they started. Pointer interaction belongs here rather than in the spec, since
 * storybook/test's userEvent is synthetic and visibility-blind.
 */
export const Cycling: Story = {
  decorators: [lightScheme],
  render: function ControlledThemeToggle() {
    const [preference, setPreference] = useState<ThemePreference>("light");
    return (
      <ThemeToggle
        data-testid="theme-toggle"
        preference={preference}
        mode={preference === "dark" ? "dark" : "light"}
        onPreferenceChange={setPreference}
      />
    );
  },
  play: async ({ canvas }) => {
    const toggle = canvas.getByTestId("theme-toggle");

    await expect(toggle.getAttribute("data-theme-preference")).toBe("light");
    await expect(toggle.getAttribute("aria-label")).toBe("Switch to dark theme");

    await userEvent.click(toggle);
    await expect(toggle.getAttribute("data-theme-preference")).toBe("dark");

    await userEvent.click(toggle);
    await expect(toggle.getAttribute("data-theme-preference")).toBe("system");
    await expect(toggle.textContent).toBe("System");

    await userEvent.click(toggle);
    await expect(toggle.getAttribute("data-theme-preference")).toBe("light");
  },
};

/**
 * The toggle on an inverted rail (Wallow-lrlm.6.4).
 *
 * The control hard-codes `variant="secondary"`, so on `bg-sidebar` it was a page
 * chip glued to the rail — L 0.92 on L 0.22 in light mode. `surface` is not one
 * of the props this component owns; it rides `ButtonProps` through the
 * passthrough down to `buttonRecipe`, and that is the whole point of the story:
 * the axis has to survive a component that never mentions it.
 *
 * ONE scheme, deliberately. This story previously rendered a `.dark` column beside
 * a `.light` one and asserted both — but per the header, a wrapper cannot move a
 * `--color-*` token, so the two columns painted the same palette and the dark half
 * re-measured light while claiming otherwise. The dark half of this criterion is
 * measured in wallow-web's `DashboardNav.sidebar-surface.test.tsx`, which stamps
 * the mode on the document element where the tokens can actually see it.
 *
 * Top is the untouched default, bottom is `surface="sidebar"`.
 */
export const OnTheSidebarSurface: Story = {
  args: { preference: "system", mode: "light" },
  decorators: [lightScheme],
  render: function ToggleOnRail(args) {
    return (
      <div className="flex w-40 flex-col gap-3 bg-sidebar p-4">
        <ThemeToggle {...args} data-testid="toggle-page" />
        <ThemeToggle {...args} data-testid="toggle-sidebar" surface="sidebar" />
      </div>
    );
  },
  play: async ({ canvas }) => {
    const pageChip = canvas.getByTestId("toggle-page");
    const sidebarChip = canvas.getByTestId("toggle-sidebar");

    // The default really is a page chip here, so the comparison below is not
    // two spellings of one colour.
    await expect(getComputedStyle(pageChip).backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(getComputedStyle(sidebarChip).backgroundColor).not.toBe(
      getComputedStyle(pageChip).backgroundColor,
    );
    await expect(getComputedStyle(sidebarChip).color).not.toBe(getComputedStyle(pageChip).color);
  },
};
