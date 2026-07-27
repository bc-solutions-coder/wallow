import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";
import { expect, fn, userEvent, waitFor } from "storybook/test";

import { Collapsible } from "./collapsible";

/*
 * Wallow-m5aq.4.1 — Collapsible stories, the standalone half of the Accordion
 * pair. Same contract as accordion.stories.tsx: `@storybook/addon-vitest` turns
 * every export into a Vitest test case rendered in the same headless Chromium as
 * the `browser` project, but WITH the real Tailwind pipeline attached, so this
 * file owns the "does the recipe actually paint" half and collapsible.test.tsx
 * owns the markup.
 *
 * THE PANEL IS HEIGHT-ANIMATED HERE AND ONLY HERE. Real CSS means the recipe's
 * 150ms `transition-[height]` genuinely runs, so no story below may assert an
 * opened panel's visibility synchronously after the press — it starts at `h-0`.
 * Every post-open assertion goes through `waitFor`, and closing is polled the
 * same way because Base UI defers the unmount until the transition ends.
 */

interface AdvancedSettingsProps {
  /** Opens the panel on first render. */
  readonly defaultOpen?: boolean;
  /** Greys out the trigger and refuses every press. */
  readonly disabled?: boolean;
  /** Keeps the closed panel in the DOM behind a `hidden` attribute. */
  readonly keepMounted?: boolean;
  /** Called with the new open state. */
  readonly onOpenChange?: (open: boolean) => void;
}

/**
 * A realistic disclosure — the story subject. As with the accordion, the panel's
 * padding lives on a wrapper INSIDE the panel: Base UI measures
 * `--collapsible-panel-height` off the panel itself, so padding there would be
 * animated too and the panel would never fully close.
 */
function AdvancedSettings({
  defaultOpen,
  disabled,
  keepMounted,
  onOpenChange,
}: AdvancedSettingsProps): ReactElement {
  return (
    <Collapsible.Root
      defaultOpen={defaultOpen}
      disabled={disabled}
      onOpenChange={onOpenChange}
      className="max-w-md"
      data-testid="settings"
    >
      <Collapsible.Trigger data-testid="trigger">
        Advanced settings
        <span aria-hidden="true">▾</span>
      </Collapsible.Trigger>
      <Collapsible.Panel keepMounted={keepMounted} data-testid="panel">
        <div className="px-3 pb-2">
          Retention policy, outbound webhooks and the API key rotation schedule.
        </div>
      </Collapsible.Panel>
    </Collapsible.Root>
  );
}

const meta = {
  title: "Components/Collapsible",
  component: AdvancedSettings,
  args: { onOpenChange: fn() },
} satisfies Meta<typeof AdvancedSettings>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Shut — the resting state, with no panel in the DOM at all. */
export const Default: Story = {};

/** Open on first render. */
export const Open: Story = {
  args: { defaultOpen: true },
};

/** The trigger greyed out — the `data-[disabled]:opacity-50` treatment. */
export const Disabled: Story = {
  args: { disabled: true },
};

/**
 * `keepMounted` keeps the closed panel in the DOM behind a `hidden` attribute,
 * which is what a caller wants when the content must stay findable or focusable
 * to something else on the page.
 */
export const KeptMounted: Story = {
  args: { keepMounted: true },
};

/** The interaction half: pressing the trigger opens the panel and reports it. */
export const Expanding: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByTestId("trigger"));

    await expect(args.onOpenChange).toHaveBeenCalledTimes(1);
    await expect(args.onOpenChange).toHaveBeenCalledWith(true, expect.anything());

    // The panel opens from h-0 through a 150ms transition — never assert
    // visibility synchronously after the press.
    await waitFor(async () => {
      await expect(canvas.getByTestId("panel")).toBeVisible();
    });
    await expect(canvas.getByTestId("trigger")).toHaveAttribute("aria-expanded", "true");
  },
};

/** And pressing again collapses it back out of the DOM. */
export const Collapsing: Story = {
  args: { defaultOpen: true },
  play: async ({ canvas }) => {
    await userEvent.click(canvas.getByTestId("trigger"));

    // Base UI defers the unmount until the collapse transition ends.
    await waitFor(() => {
      expect(canvas.queryByTestId("panel")).toBeNull();
    });
    await expect(canvas.getByTestId("trigger")).toHaveAttribute("aria-expanded", "false");
  },
};

/** Proves the recipe utilities actually reached the element as CSS. */
export const PaintedByTheDesignTokens: Story = {
  args: { defaultOpen: true },
  play: async ({ canvas }) => {
    const panel = canvas.getByTestId("panel");
    const trigger = canvas.getByTestId("trigger");

    const panelStyle = getComputedStyle(panel);
    await expect(panelStyle.overflow).toBe("hidden");
    await expect(panelStyle.transitionProperty).toContain("height");
    await expect(panelStyle.transitionDuration).toBe("0.15s");

    await expect(getComputedStyle(trigger).color).not.toBe("");
    await expect(getComputedStyle(trigger).transitionProperty).toContain("color");
  },
};
