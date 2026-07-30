import type { Decorator, Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";
import { useState } from "react";

import type { ThemePreference } from "../theme-provider";
import { ThemeToggle } from "./theme-toggle";

/*
 * Wallow-lrlm.1.2 — ThemeToggle stories. Each export becomes a Vitest test case
 * in the same headless Chromium the `browser` project uses, but with the real
 * Tailwind pipeline and the fork's real theme attached (.storybook/preview.css +
 * preview.tsx), so this is the only place the control's secondary-token colours
 * can actually be seen — in BOTH schemes.
 *
 * Every story renders the toggle CONTROLLED (`preference` + `mode` supplied), so
 * no story touches `document.documentElement`, `localStorage`, or a real
 * `ThemeProvider`. Stories share one document; a story that stamped the real
 * theme class would leak into every story after it. The `.dark` wrapper below
 * scopes the dark scheme to the story's own subtree instead — the token blocks
 * `renderThemeStyle` emits are class-scoped, so a wrapper is enough.
 *
 * Callback spies come from `fn()` in `storybook/test` (never `vi.fn()`, which
 * the Interactions panel cannot display).
 */

/** Renders the story inside the fork's dark scheme, scoped to this subtree. */
const darkScheme: Decorator = (Story) => (
  <div className="dark bg-background text-foreground p-6">
    <Story />
  </div>
);

/** Renders the story inside the fork's light scheme, scoped to this subtree. */
const lightScheme: Decorator = (Story) => (
  <div className="light bg-background text-foreground p-6">
    <Story />
  </div>
);

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
};

/** The visitor explicitly chose dark. */
export const Dark: Story = {
  args: { preference: "dark", mode: "dark" },
  decorators: [darkScheme],
};

/**
 * The default: no choice made, so the OS decides. The label reads `System` while
 * the control renders in whatever scheme that currently resolves to — here the
 * light one.
 */
export const SystemLight: Story = {
  args: { preference: "system", mode: "light" },
  decorators: [lightScheme],
};

/** The same `system` preference, resolving the other way. */
export const SystemDark: Story = {
  args: { preference: "system", mode: "dark" },
  decorators: [darkScheme],
};

/** Disabled, so the fork can suppress the control without hiding it. */
export const Disabled: Story = {
  args: { preference: "light", mode: "light", disabled: true },
  decorators: [lightScheme],
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
