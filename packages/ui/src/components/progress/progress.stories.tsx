import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, userEvent } from "storybook/test";

import { Progress } from "./progress";

/*
 * Wallow-m5aq.4.4 — Progress stories. `@storybook/addon-vitest` turns every
 * export below into a Vitest test case rendered in the same headless Chromium
 * the `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while progress.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Nothing about Progress is portalled or animated open, so every play function
 * queries `canvas` and none of the Wave-2 waitFor-after-opening rule applies —
 * the only thing that moves here is a width, and it moves synchronously with a
 * React re-render.
 *
 * The interaction stories use storybook/test's `userEvent`, which is
 * @testing-library/user-event and dispatches synthetic events, so a click needs
 * no hit-testing.
 */

interface UploadProgressProps {
  /** The completion value, or `null` for an indeterminate bar. */
  readonly value?: number | null;
  /** The bottom of the range. */
  readonly min?: number;
  /** The top of the range — `value === max` is what makes the bar `data-complete`. */
  readonly max?: number;
  /** `Intl.NumberFormat` options for the visible readout. */
  readonly format?: Intl.NumberFormatOptions;
  /** Renders a button that advances the value, for the interaction stories. */
  readonly withStepper?: boolean;
  /** Called with the value each step lands on. */
  readonly onStep?: (value: number) => void;
}

/**
 * A complete, realistic upload bar — the story subject. Stories drive the real
 * `Progress` namespace through this so every part is exercised together rather
 * than one part at a time. The label/value row is the caller's own `div`: the
 * root recipe only stacks, exactly like Slider's.
 */
function UploadProgress({
  value = 40,
  min,
  max,
  format,
  withStepper,
  onStep,
}: UploadProgressProps): ReactElement {
  const [current, setCurrent] = useState(value);

  return (
    <div className="w-80">
      <Progress.Root value={current} min={min} max={max} format={format} data-testid="upload">
        <div className="flex items-baseline justify-between">
          <Progress.Label data-testid="upload-label">Uploading files</Progress.Label>
          <Progress.Value data-testid="upload-value" />
        </div>
        <Progress.Track data-testid="upload-track">
          <Progress.Indicator data-testid="upload-indicator" />
        </Progress.Track>
      </Progress.Root>
      {withStepper ? (
        <button
          type="button"
          data-testid="upload-step"
          onClick={() => {
            const next = Math.min((current ?? 0) + 30, max ?? 100);
            setCurrent(next);
            onStep?.(next);
          }}
        >
          Advance
        </button>
      ) : null}
    </div>
  );
}

const meta = {
  title: "Components/Progress",
  component: UploadProgress,
  args: { onStep: fn() },
} satisfies Meta<typeof UploadProgress>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The everyday state: a partially filled bar, `data-progressing`. */
export const Default: Story = {};

/** `value === max`, which is what flips every part to `data-complete`. */
export const Complete: Story = {
  args: { value: 100 },
};

/**
 * `value={null}`. Base UI writes NO inline width here, which is the only reason
 * the indicator recipe's `data-[indeterminate]:w-full` can take effect.
 */
export const Indeterminate: Story = {
  args: { value: null },
};

/**
 * A custom range. Worth its own story because the two halves disagree on
 * purpose: the Indicator is 25% wide (5 of 0..20) while the Value reads "5%"
 * (the raw number as a percent). That is Base UI's contract, pinned here so a
 * later "fix" has to argue with a story.
 */
export const CustomRange: Story = {
  args: { value: 5, min: 0, max: 20 },
};

/** The readout run through `Intl.NumberFormat` instead of the default percent. */
export const WithFormat: Story = {
  args: { value: 0.4, format: { style: "percent", minimumFractionDigits: 1 } },
};

/**
 * The interaction half: advancing the value moves the indicator and, at the top
 * of the range, swaps the status attribute every recipe keys off.
 *
 * The width is read off `style.width` rather than through `toHaveStyle`, which
 * compares COMPUTED values and would resolve Base UI's "40%" to the pixel width
 * of whatever box the story happens to be laid out in.
 */
export const Advancing: Story = {
  args: { value: 40, withStepper: true },
  play: async ({ args, canvas }) => {
    const indicator = canvas.getByTestId("upload-indicator");
    await expect(indicator.style.width).toBe("40%");

    await userEvent.click(canvas.getByTestId("upload-step"));

    await expect(args.onStep).toHaveBeenCalledWith(70);
    await expect(indicator.style.width).toBe("70%");
    await expect(canvas.getByTestId("upload-value")).toHaveTextContent("70%");
    await expect(canvas.getByTestId("upload")).toHaveAttribute("data-progressing");

    await userEvent.click(canvas.getByTestId("upload-step"));

    await expect(canvas.getByTestId("upload")).toHaveAttribute("data-complete");
    await expect(indicator.style.width).toBe("100%");
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of well-formed
 * but non-existent utility names passes every class-set assertion in
 * progress.test.tsx and still paints nothing.
 *
 * Two of these assertions cannot be made anywhere else in the suite:
 *   - the indicator's painted width must come out at the fraction of the track
 *     Base UI wrote inline. That is the proof the fill is wired to the value
 *     rather than to a fixed size, and it also proves the track's `w-full`
 *     resolved to a real box for the percentage to measure against.
 *   - the indicator's `height: inherit` only resolves to something visible
 *     because the TRACK recipe sets an explicit height. Drop `h-2` from the
 *     track and the bar silently collapses to nothing while every markup
 *     assertion still passes.
 */
export const PaintedByTheDesignTokens: Story = {
  play: async ({ canvas }) => {
    const track = canvas.getByTestId("upload-track");
    const trackStyle = getComputedStyle(track);
    await expect(Math.round(parseFloat(trackStyle.height))).toBe(8);
    await expect(trackStyle.overflowX).toBe("hidden");
    await expect(trackStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(parseFloat(trackStyle.borderTopLeftRadius)).toBeGreaterThan(0);

    const indicator = canvas.getByTestId("upload-indicator");
    const indicatorStyle = getComputedStyle(indicator);
    // The fill has to READ as a fill: a different colour from the rail it sits in.
    await expect(indicatorStyle.backgroundColor).not.toBe(trackStyle.backgroundColor);
    await expect(indicatorStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(Math.round(parseFloat(indicatorStyle.height))).toBe(8);
    await expect(Math.round(indicator.getBoundingClientRect().width)).toBe(
      Math.round(track.getBoundingClientRect().width * 0.4),
    );
    // The indeterminate-only utilities must be genuinely SCOPED: a determinate
    // bar that pulsed would be this component's most annoying possible bug.
    await expect(indicatorStyle.animationName).toBe("none");

    // `text-muted-foreground` vs `text-foreground`: the readout has to sit back
    // from the label, or the row reads as two equal headings.
    const label = canvas.getByTestId("upload-label");
    const value = canvas.getByTestId("upload-value");
    await expect(getComputedStyle(label).color).not.toBe(getComputedStyle(value).color);
    await expect(getComputedStyle(label).fontWeight).not.toBe("400");
  },
};

/**
 * The indeterminate bar's paint, which is a different proof from the story
 * above: with no inline width, `data-[indeterminate]:w-full` has to be what
 * fills the rail — and it is the only utility in the whole catalog that depends
 * on Base UI writing NOTHING.
 *
 * The width alone would prove nothing (an unstyled `div` already fills its
 * block parent), so the two assertions that actually bite are the animation —
 * `data-[indeterminate]:animate-pulse` resolving to a real keyframes rule — and
 * the fill colour, neither of which an empty recipe can produce.
 */
export const IndeterminatePaintsFullWidth: Story = {
  args: { value: null },
  play: async ({ canvas }) => {
    const track = canvas.getByTestId("upload-track");
    const indicator = canvas.getByTestId("upload-indicator");
    const indicatorStyle = getComputedStyle(indicator);

    await expect(indicator.style.width).toBe("");
    await expect(Math.round(indicator.getBoundingClientRect().width)).toBe(
      Math.round(track.getBoundingClientRect().width),
    );
    await expect(indicatorStyle.animationName).not.toBe("none");
    await expect(indicatorStyle.backgroundColor).not.toBe(getComputedStyle(track).backgroundColor);
  },
};
