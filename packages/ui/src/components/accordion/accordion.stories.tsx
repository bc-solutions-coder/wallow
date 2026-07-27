import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";
import { expect, fn, userEvent, waitFor } from "storybook/test";

import { Accordion, type AccordionRootProps } from "./accordion";

/*
 * Wallow-m5aq.4.1 — Accordion stories. `@storybook/addon-vitest` turns every
 * export below into a Vitest test case rendered in the same headless Chromium
 * the `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while accordion.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Two things belong HERE rather than in accordion.test.tsx:
 *
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens). This project compiles real Tailwind; the
 *     `browser` project does not, so there `h-[var(--accordion-panel-height)]`
 *     and `transition-[height]` are inert strings on a class list.
 *   - THE PANEL IS HEIGHT-ANIMATED HERE AND ONLY HERE. Because real CSS is
 *     loaded, the recipe's 150ms `transition-[height]` genuinely runs, so no
 *     story below may assert an opened panel's visibility or height
 *     SYNCHRONOUSLY after the press — it starts at `h-0`. Every post-open
 *     assertion goes through `waitFor` (the Wallow-m5aq.3.1 exemplar's gotcha,
 *     which cost that task a red run), and closing is likewise polled through
 *     `waitFor` because Base UI defers the unmount until the transition ends.
 */

interface FaqProps {
  /** Lets several panels stay open at once. */
  readonly multiple?: boolean;
  /** The items open on first render. */
  readonly defaultValue?: string[];
  /** Greys out the whole accordion and refuses every press. */
  readonly disabled?: boolean;
  /** Called with the whole new value array whenever an item opens or closes. */
  readonly onValueChange?: AccordionRootProps["onValueChange"];
}

/**
 * A complete, realistic accordion — the story subject. Stories drive the real
 * `Accordion` namespace through this so every part is exercised together rather
 * than one part at a time.
 *
 * The panel's padding lives on a wrapper INSIDE the panel, never on the panel
 * itself: Base UI measures `--accordion-panel-height` off the panel, so padding
 * there would be animated too and the panel would never fully close.
 */
function Faq({ multiple, defaultValue, disabled, onValueChange }: FaqProps): ReactElement {
  return (
    <Accordion.Root
      multiple={multiple}
      defaultValue={defaultValue}
      disabled={disabled}
      onValueChange={onValueChange}
      className="max-w-md"
      data-testid="faq"
    >
      <Accordion.Item value="shipping">
        <Accordion.Header>
          <Accordion.Trigger data-testid="trigger-shipping">
            How fast do you ship?
            <span aria-hidden="true">+</span>
          </Accordion.Trigger>
        </Accordion.Header>
        <Accordion.Panel data-testid="panel-shipping">
          <div className="px-4 pb-3">Orders placed before 4pm leave the same day.</div>
        </Accordion.Panel>
      </Accordion.Item>
      <Accordion.Item value="returns">
        <Accordion.Header>
          <Accordion.Trigger data-testid="trigger-returns">
            What is the return window?
            <span aria-hidden="true">+</span>
          </Accordion.Trigger>
        </Accordion.Header>
        <Accordion.Panel data-testid="panel-returns">
          <div className="px-4 pb-3">Thirty days from delivery, no questions asked.</div>
        </Accordion.Panel>
      </Accordion.Item>
      <Accordion.Item value="support">
        <Accordion.Header>
          <Accordion.Trigger data-testid="trigger-support">
            How do I reach support?
            <span aria-hidden="true">+</span>
          </Accordion.Trigger>
        </Accordion.Header>
        <Accordion.Panel data-testid="panel-support">
          <div className="px-4 pb-3">Reply to any order email and a human answers.</div>
        </Accordion.Panel>
      </Accordion.Item>
    </Accordion.Root>
  );
}

const meta = {
  title: "Components/Accordion",
  component: Faq,
  args: { onValueChange: fn() },
} satisfies Meta<typeof Faq>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Everything shut — the accordion's resting state. */
export const Default: Story = {};

/** One panel open on first render, the single-select default. */
export const Open: Story = {
  args: { defaultValue: ["shipping"] },
};

/** Several panels open at once. */
export const Multiple: Story = {
  args: { multiple: true, defaultValue: ["shipping", "returns"] },
};

/** The whole accordion greyed out — the `data-[disabled]:opacity-50` treatment. */
export const Disabled: Story = {
  args: { disabled: true, defaultValue: ["shipping"] },
};

/** The interaction half: pressing a trigger opens its panel and reports the value. */
export const Expanding: Story = {
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByTestId("trigger-shipping"));

    await expect(args.onValueChange).toHaveBeenCalledTimes(1);
    await expect(args.onValueChange).toHaveBeenCalledWith(["shipping"], expect.anything());

    // The panel opens from h-0 through a 150ms transition, so visibility is
    // only true once that settles — never assert it synchronously here.
    await waitFor(async () => {
      await expect(canvas.getByTestId("panel-shipping")).toBeVisible();
    });
    await expect(canvas.getByTestId("trigger-shipping")).toHaveAttribute("aria-expanded", "true");
  },
};

/** Single-select: opening one panel collapses the one that was open. */
export const OpeningOneClosesTheOther: Story = {
  args: { defaultValue: ["shipping"] },
  play: async ({ canvas }) => {
    await userEvent.click(canvas.getByTestId("trigger-returns"));

    await waitFor(async () => {
      await expect(canvas.getByTestId("panel-returns")).toBeVisible();
    });
    // Base UI defers the unmount until the collapse transition ends, so the old
    // panel is still in the DOM for a beat after the press.
    await waitFor(() => {
      expect(canvas.queryByTestId("panel-shipping")).toBeNull();
    });
  },
};

/** Proves the recipe utilities actually reached the element as CSS, which only a
 * project with the real Tailwind pipeline can show. */
export const PaintedByTheDesignTokens: Story = {
  args: { defaultValue: ["shipping"] },
  play: async ({ canvas }) => {
    const panel = canvas.getByTestId("panel-shipping");
    const trigger = canvas.getByTestId("trigger-shipping");

    // `overflow-hidden` is what makes the clipped content read as collapsing,
    // and `transition-[height]` is what makes it take 150ms rather than snap.
    const panelStyle = getComputedStyle(panel);
    await expect(panelStyle.overflow).toBe("hidden");
    await expect(panelStyle.transitionProperty).toContain("height");
    await expect(panelStyle.transitionDuration).toBe("0.15s");

    // A resolved colour proves the semantic token reached CSS rather than
    // sitting on the class list as an unmatched utility.
    await expect(getComputedStyle(trigger).color).not.toBe("");
    await expect(getComputedStyle(trigger).transitionProperty).toContain("color");
  },
};
