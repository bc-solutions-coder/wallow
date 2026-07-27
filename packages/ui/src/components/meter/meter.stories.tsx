import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, userEvent } from "storybook/test";

import { Meter } from "./meter";

/*
 * Wallow-m5aq.4.4 — Meter stories, the paired half of the Progress task.
 * `@storybook/addon-vitest` turns every export below into a Vitest test case
 * rendered in the same headless Chromium the `browser` project uses, with the
 * real Tailwind pipeline attached (see .storybook/main.ts), so these are the
 * VISUAL half of the component's spec while meter.test.tsx holds the markup
 * assertions a screenshot cannot make.
 *
 * Nothing about Meter is portalled or animated open, so every play function
 * queries `canvas` and none of the Wave-2 waitFor-after-opening rule applies.
 *
 * The interaction story uses storybook/test's `userEvent`, which is
 * @testing-library/user-event and dispatches synthetic events, so a click needs
 * no hit-testing.
 */

interface StorageMeterProps {
  /** The reading. Outside `min`..`max` it is clamped, not rejected. */
  readonly value?: number;
  /** The bottom of the range. */
  readonly min?: number;
  /** The top of the range. */
  readonly max?: number;
  /** `Intl.NumberFormat` options — switches the readout from the position to the raw value. */
  readonly format?: Intl.NumberFormatOptions;
  /** Renders a button that raises the reading, for the interaction story. */
  readonly withStepper?: boolean;
  /** Called with the value each step lands on. */
  readonly onStep?: (value: number) => void;
}

/**
 * A realistic storage-quota meter — the story subject. Stories drive the real
 * `Meter` namespace through this so every part is exercised together rather
 * than one part at a time. The label/value row is the caller's own `div`: the
 * root recipe only stacks, exactly like Slider's.
 */
function StorageMeter({
  value = 30,
  min,
  max,
  format,
  withStepper,
  onStep,
}: StorageMeterProps): ReactElement {
  const [current, setCurrent] = useState(value);

  return (
    <div className="w-80">
      <Meter.Root value={current} min={min} max={max} format={format} data-testid="storage">
        <div className="flex items-baseline justify-between">
          <Meter.Label data-testid="storage-label">Storage used</Meter.Label>
          <Meter.Value data-testid="storage-value" />
        </div>
        <Meter.Track data-testid="storage-track">
          <Meter.Indicator data-testid="storage-indicator" />
        </Meter.Track>
      </Meter.Root>
      {withStepper ? (
        <button
          type="button"
          data-testid="storage-step"
          onClick={() => {
            const next = current + 40;
            setCurrent(next);
            onStep?.(next);
          }}
        >
          Add 40
        </button>
      ) : null}
    </div>
  );
}

const meta = {
  title: "Components/Meter",
  component: StorageMeter,
  args: { onStep: fn() },
} satisfies Meta<typeof StorageMeter>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The everyday state: a reading a third of the way along its range. */
export const Default: Story = {};

/** An empty meter — the indicator is still in the DOM, just zero-width. */
export const Empty: Story = {
  args: { value: 0 },
};

/** A full meter. There is no `data-complete` here; a meter has no states. */
export const Full: Story = {
  args: { value: 100 },
};

/**
 * A range that does not start at zero. 60 of 50..100 fills a FIFTH of the track
 * and reads "20%", because both halves report the position in the range.
 */
export const OffsetRange: Story = {
  args: { value: 60, min: 50, max: 100 },
};

/**
 * The readout run through `Intl.NumberFormat`. Worth its own story because
 * `format` changes what the readout MEANS: it reports the raw quantity (30 GB)
 * and stops agreeing with the fill (60% of a 50 GB quota).
 */
export const WithFormat: Story = {
  args: { value: 30, max: 50, format: { style: "unit", unit: "gigabyte" } },
};

/**
 * The interaction half, and the clamping contract: pushing the reading past
 * `max` fills the track and stops there rather than overflowing it.
 *
 * The width is read off `style.width` rather than through `toHaveStyle`, which
 * compares COMPUTED values and would resolve Base UI's "60%" to the pixel width
 * of whatever box the story happens to be laid out in.
 */
export const ClampsAtTheTopOfTheRange: Story = {
  args: { value: 30, max: 50, withStepper: true },
  play: async ({ args, canvas }) => {
    const indicator = canvas.getByTestId("storage-indicator");
    await expect(indicator.style.width).toBe("60%");

    await userEvent.click(canvas.getByTestId("storage-step"));

    await expect(args.onStep).toHaveBeenCalledWith(70);
    await expect(indicator.style.width).toBe("100%");
    await expect(canvas.getByTestId("storage-value")).toHaveTextContent("100%");
    await expect(canvas.getByTestId("storage")).toHaveAttribute("aria-valuenow", "50");
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of well-formed
 * but non-existent utility names passes every class-set assertion in
 * meter.test.tsx and still paints nothing.
 *
 * Two of these assertions cannot be made anywhere else in the suite:
 *   - the indicator's painted width must come out at the fraction of the track
 *     Base UI wrote inline, which also proves the track's `w-full` resolved to a
 *     real box for that percentage to measure against.
 *   - the indicator's `height: inherit` only resolves to something visible
 *     because the TRACK recipe sets an explicit height. Drop `h-2` from the
 *     track and the meter silently collapses to nothing while every markup
 *     assertion still passes.
 */
export const PaintedByTheDesignTokens: Story = {
  play: async ({ canvas }) => {
    const track = canvas.getByTestId("storage-track");
    const trackStyle = getComputedStyle(track);
    await expect(Math.round(parseFloat(trackStyle.height))).toBe(8);
    await expect(trackStyle.overflowX).toBe("hidden");
    await expect(trackStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(parseFloat(trackStyle.borderTopLeftRadius)).toBeGreaterThan(0);

    const indicator = canvas.getByTestId("storage-indicator");
    const indicatorStyle = getComputedStyle(indicator);
    // The fill has to READ as a fill: a different colour from the rail it sits in.
    await expect(indicatorStyle.backgroundColor).not.toBe(trackStyle.backgroundColor);
    await expect(indicatorStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(Math.round(parseFloat(indicatorStyle.height))).toBe(8);
    await expect(Math.round(indicator.getBoundingClientRect().width)).toBe(
      Math.round(track.getBoundingClientRect().width * 0.3),
    );

    // `text-muted-foreground` vs `text-foreground`: the readout has to sit back
    // from the label, or the row reads as two equal headings.
    const label = canvas.getByTestId("storage-label");
    const value = canvas.getByTestId("storage-value");
    await expect(getComputedStyle(label).color).not.toBe(getComputedStyle(value).color);
    await expect(getComputedStyle(label).fontWeight).not.toBe("400");
  },
};
