import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent } from "storybook/test";

import { Slider } from "./slider";

/*
 * The VISUAL half of the Slider spec. Unlike `slider.test.tsx`, these render
 * under the real Tailwind pipeline (`.storybook/preview.css`) against the fork's
 * real theme, so this is the only place the rail's thickness, the indicator's
 * fill and the thumb's position can actually be seen — and the only place a
 * POINTER interaction means anything, since Base UI derives the value a press
 * lands on from the control's bounding box, which is 0x0 without a stylesheet.
 *
 * `@storybook/addon-vitest` runs each export below as a Vitest test case, and
 * callback spies come from `fn()` in `storybook/test` (never `vi.fn()`, which
 * the Interactions panel cannot display).
 */

const meta = {
  title: "Components/Slider",
  component: Slider.Root,
  args: { onValueChange: fn(), defaultValue: 40 },
  render: (args) => (
    <div className="w-64">
      <Slider.Root {...args}>
        <Slider.Label>Volume</Slider.Label>
        <Slider.Value data-testid="slider-value" />
        <Slider.Control data-testid="slider-control">
          <Slider.Track>
            <Slider.Indicator />
            <Slider.Thumb data-testid="slider-thumb" />
          </Slider.Track>
        </Slider.Control>
      </Slider.Root>
    </div>
  ),
} satisfies Meta<typeof Slider.Root>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};

/** The two ends, where the thumb has to stay inside the control rather than overhang it. */
export const AtMinimum: Story = {
  args: { defaultValue: 0 },
};

export const AtMaximum: Story = {
  args: { defaultValue: 100 },
};

export const Disabled: Story = {
  args: { disabled: true },
};

/** A coarse slider: the thumb may only land on multiples of the step. */
export const Stepped: Story = {
  args: { defaultValue: 40, step: 20 },
};

/** The readout is formatted through `Intl.NumberFormat`, not hand-rolled by the caller. */
export const Percentage: Story = {
  args: { defaultValue: 0.4, max: 1, step: 0.01, format: { style: "percent" } },
};

/** Two thumbs sharing one indicator — the indicator spans BETWEEN them, not from min. */
export const Range: Story = {
  args: { defaultValue: [20, 60] },
  render: (args) => (
    <div className="w-64">
      <Slider.Root {...args}>
        <Slider.Label>Price</Slider.Label>
        <Slider.Value />
        <Slider.Control>
          <Slider.Track>
            <Slider.Indicator />
            <Slider.Thumb index={0} />
            <Slider.Thumb index={1} />
          </Slider.Track>
        </Slider.Control>
      </Slider.Root>
    </div>
  ),
};

/** The vertical geometry the `data-[orientation=vertical]` utilities exist for. */
export const Vertical: Story = {
  args: { orientation: "vertical" },
};

/**
 * The interaction half: a real press on the control moves the thumb to where it
 * landed. This is the assertion `slider.test.tsx` cannot make — it needs a
 * control with a real bounding box, which only the Storybook project has.
 */
export const PressingTheTrack: Story = {
  play: async ({ args, canvas }) => {
    const control = canvas.getByTestId("slider-control");
    const value = canvas.getByTestId("slider-value");

    await expect(value).toHaveTextContent("40");

    const box = control.getBoundingClientRect();
    await userEvent.pointer([
      {
        keys: "[MouseLeft]",
        target: control,
        coords: { clientX: box.left + box.width * 0.75, clientY: box.top + box.height / 2 },
      },
    ]);

    await expect(args.onValueChange).toHaveBeenCalled();
    await expect(value).not.toHaveTextContent("40");
  },
};
