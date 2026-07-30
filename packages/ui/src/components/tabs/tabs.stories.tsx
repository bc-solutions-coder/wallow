import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";
import { expect, fn, userEvent, waitFor } from "storybook/test";

import { Tabs } from "./tabs";

/*
 * Wallow-m5aq.4.2 — Tabs stories. `@storybook/addon-vitest` turns every export
 * below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while tabs.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Nothing about Tabs is portalled, so every play function queries `canvas`.
 *
 * The interaction stories use storybook/test's `userEvent`, which is
 * @testing-library/user-event and dispatches synthetic events, so a click needs
 * no hit-testing. The keyboard path is still the one worth pinning here: the
 * arrow key only moves the roving tab stop and Enter is what commits, which is
 * the whole reason this component has a `List` rather than a row of buttons.
 */

interface SettingsTabsProps {
  /** The tab active on first render, or `null` for "no tab active". */
  readonly defaultValue?: string | null;
  /** Lays the strip out as a vertical rail beside the panels. */
  readonly orientation?: "horizontal" | "vertical";
  /** Makes the arrow keys activate as they move instead of only moving focus. */
  readonly activateOnFocus?: boolean;
  /** Keeps inactive panels mounted (`hidden` + `inert`) instead of unmounting them. */
  readonly keepMounted?: boolean;
  /** Renders the third tab disabled. */
  readonly withDisabledTab?: boolean;
  /** Called with the newly active tab's value. */
  readonly onValueChange?: (value: string) => void;
}

/**
 * A complete, realistic tab set — the story subject. Stories drive the real
 * `Tabs` namespace through this so every part is exercised together rather than
 * one part at a time.
 */
function SettingsTabs({
  defaultValue = "account",
  orientation,
  activateOnFocus,
  keepMounted,
  withDisabledTab,
  onValueChange,
}: SettingsTabsProps): ReactElement {
  return (
    <Tabs.Root
      defaultValue={defaultValue}
      orientation={orientation}
      onValueChange={onValueChange}
      data-testid="settings"
    >
      <Tabs.List activateOnFocus={activateOnFocus} data-testid="settings-list">
        <Tabs.Tab value="account" data-testid="tab-account">
          Account
        </Tabs.Tab>
        <Tabs.Tab value="password" data-testid="tab-password">
          Password
        </Tabs.Tab>
        <Tabs.Tab value="billing" disabled={withDisabledTab} data-testid="tab-billing">
          Billing
        </Tabs.Tab>
        <Tabs.Indicator data-testid="settings-indicator" />
      </Tabs.List>
      <Tabs.Panel value="account" keepMounted={keepMounted} data-testid="panel-account">
        Your name, email address and profile photo.
      </Tabs.Panel>
      <Tabs.Panel value="password" keepMounted={keepMounted} data-testid="panel-password">
        Change your password and manage two-factor authentication.
      </Tabs.Panel>
      <Tabs.Panel value="billing" keepMounted={keepMounted} data-testid="panel-billing">
        Your plan, payment method and invoices.
      </Tabs.Panel>
    </Tabs.Root>
  );
}

const meta = {
  title: "Components/Tabs",
  component: SettingsTabs,
  args: { onValueChange: fn() },
} satisfies Meta<typeof SettingsTabs>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The everyday state: a strip of tabs with the first one active. */
export const Default: Story = {};

/** The strip as a rail, which is what every `data-[orientation=vertical]:` modifier styles. */
export const Vertical: Story = {
  args: { orientation: "vertical" },
};

/** A tab that cannot be activated — greyed by the recipe's `data-[disabled]:` modifier. */
export const WithDisabledTab: Story = {
  args: { withDisabledTab: true },
};

/**
 * Inactive panels kept in the DOM. The panel recipe sets no unprefixed `display`
 * utility, so the `hidden` attribute Base UI adds still wins; only
 * PaintedByTheDesignTokens can prove that, because it needs a real stylesheet.
 */
export const KeepMounted: Story = {
  args: { keepMounted: true },
};

/** Nothing active: the Indicator renders no element at all. */
export const NoActiveTab: Story = {
  args: { defaultValue: null },
};

/** The interaction half: clicking a tab swaps which panel is mounted. */
export const SelectingATab: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByTestId("tab-password"));

    await expect(args.onValueChange).toHaveBeenCalledWith("password", expect.anything());
    await waitFor(() => {
      expect(canvas.getByTestId("panel-password")).toBeVisible();
    });
    // The outgoing panel leaves on Base UI's transition lifecycle, so it is
    // still in the DOM (`inert`, `data-ending-style`) for a beat after the new
    // one paints — the unmount has to be waited for, not asserted inline.
    await waitFor(() => {
      expect(canvas.queryByTestId("panel-account")).toBeNull();
    });
  },
};

/**
 * The keyboard contract, and the reason this is a composite widget: the arrow
 * key moves the single tab stop WITHOUT activating, and Enter commits.
 */
export const KeyboardNavigation: Story = {
  play: async ({ args, canvas }) => {
    canvas.getByTestId("tab-account").focus();

    await userEvent.keyboard("{ArrowRight}");
    await expect(canvas.getByTestId("tab-password")).toHaveFocus();
    await expect(canvas.getByTestId("tab-password")).toHaveAttribute("aria-selected", "false");
    await expect(args.onValueChange).not.toHaveBeenCalled();

    await userEvent.keyboard("{Enter}");
    await expect(args.onValueChange).toHaveBeenCalledWith("password", expect.anything());
    await waitFor(() => {
      expect(canvas.getByTestId("panel-password")).toBeVisible();
    });
  },
};

/** With `activateOnFocus`, the arrow key alone commits. */
export const ActivateOnFocus: Story = {
  args: { activateOnFocus: true },
  play: async ({ args, canvas }) => {
    canvas.getByTestId("tab-account").focus();

    await userEvent.keyboard("{ArrowRight}");

    await expect(args.onValueChange).toHaveBeenCalledWith("password", expect.anything());
    await expect(canvas.getByTestId("tab-password")).toHaveAttribute("aria-selected", "true");
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of well-formed
 * but non-existent utility names passes every class-set assertion in
 * tabs.test.tsx and still paints nothing.
 *
 * Two of these assertions cannot be made anywhere else in the suite:
 *   - the Indicator's width is `var(--active-tab-width)`, so it must come out
 *     equal to the active tab's own width. That is the one proof the sliding
 *     rule is wired to Base UI's measurements rather than to a fixed size.
 *   - a `keepMounted` inactive panel must still compute to `display: none`. A
 *     `flex`/`block` in the panel recipe would beat the UA rule for the `hidden`
 *     attribute and reveal it.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { keepMounted: true },
  play: async ({ canvas }) => {
    const list = canvas.getByTestId("settings-list");
    const listStyle = getComputedStyle(list);
    await expect(listStyle.display).toBe("flex");
    await expect(listStyle.position).toBe("relative");
    await expect(listStyle.borderBottomWidth).not.toBe("0px");

    // `text-muted-foreground` vs `data-[active]:text-foreground`: the active tab
    // has to read differently from its neighbours, or the strip says nothing.
    const activeTab = canvas.getByTestId("tab-account");
    const idleTab = canvas.getByTestId("tab-password");
    await expect(getComputedStyle(activeTab).color).not.toBe(getComputedStyle(idleTab).color);
    await expect(getComputedStyle(activeTab).paddingLeft).not.toBe("0px");

    const indicator = canvas.getByTestId("settings-indicator");
    const indicatorStyle = getComputedStyle(indicator);
    await expect(indicatorStyle.position).toBe("absolute");
    await expect(indicatorStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(Math.round(parseFloat(indicatorStyle.height))).toBe(2);
    await expect(Math.round(parseFloat(indicatorStyle.width))).toBe(
      Math.round(activeTab.getBoundingClientRect().width),
    );

    await expect(getComputedStyle(canvas.getByTestId("panel-password")).display).toBe("none");
    await expect(getComputedStyle(canvas.getByTestId("panel-account")).display).not.toBe("none");
  },
};
