import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";
import { expect, fn, userEvent } from "storybook/test";

import { Toolbar } from "./toolbar";

/*
 * Wallow-m5aq.4.5 — Toolbar stories. `@storybook/addon-vitest` turns every
 * export below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec while
 * toolbar.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Nothing about Toolbar is portalled, so every play function queries `canvas`.
 */

interface FormattingToolbarProps {
  /** Which way the strip runs; the separator renders the opposite way. */
  readonly orientation?: "horizontal" | "vertical";
  /** Greys out and blocks every control at once. */
  readonly disabled?: boolean;
  /** Renders the italic control disabled — still focusable, by Base UI's default. */
  readonly italicDisabled?: boolean;
  readonly onBold?: () => void;
  readonly onItalic?: () => void;
}

/**
 * A realistic editor strip — the story subject. Stories drive the real `Toolbar`
 * namespace through this so every part is exercised together rather than one part
 * at a time.
 */
function FormattingToolbar({
  orientation,
  disabled,
  italicDisabled,
  onBold,
  onItalic,
}: FormattingToolbarProps): ReactElement {
  return (
    <Toolbar.Root
      data-testid="format"
      aria-label="Text formatting"
      orientation={orientation}
      disabled={disabled}
    >
      <Toolbar.Group data-testid="format-group">
        <Toolbar.Button data-testid="format-bold" onClick={onBold}>
          Bold
        </Toolbar.Button>
        <Toolbar.Button data-testid="format-italic" onClick={onItalic} disabled={italicDisabled}>
          Italic
        </Toolbar.Button>
      </Toolbar.Group>
      <Toolbar.Separator data-testid="format-separator" />
      <Toolbar.Input data-testid="format-search" aria-label="Find in document" placeholder="Find" />
      <Toolbar.Link data-testid="format-help" href="https://example.com/help">
        Help
      </Toolbar.Link>
    </Toolbar.Root>
  );
}

const meta = {
  title: "Components/Toolbar",
  component: FormattingToolbar,
} satisfies Meta<typeof FormattingToolbar>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The everyday strip: two controls, a rule, a field and a link. */
export const Default: Story = {};

/** The same parts stacked, with the rule turning horizontal to cross the column. */
export const Vertical: Story = {
  args: { orientation: "vertical" },
};

/** Every control dimmed and inert, without any of them leaving the layout. */
export const Disabled: Story = {
  args: { disabled: true },
};

/** One dimmed control among live ones — still reachable, still not activatable. */
export const WithDisabledItem: Story = {
  args: { italicDisabled: true },
};

/**
 * The interaction half: one Tab reaches the strip, and the arrows move within it.
 * This is what makes the component a toolbar rather than a row of buttons.
 */
export const ArrowKeysMoveWithinTheStrip: Story = {
  args: { onItalic: fn() },
  play: async ({ canvas, args }) => {
    canvas.getByTestId("format-bold").focus();
    await userEvent.keyboard("{ArrowRight}");

    await expect(canvas.getByTestId("format-italic")).toHaveFocus();

    await userEvent.keyboard("{Enter}");
    await expect(args.onItalic).toHaveBeenCalledTimes(1);
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to a
 * `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of well-formed
 * but non-existent utility names passes every class-set assertion in
 * toolbar.test.tsx and still paints nothing.
 *
 * Three of these assertions cannot be made anywhere else in the suite:
 *   - the strip is a real ROW: `flex` has to resolve, or the controls stack and
 *     the arrow keys stop matching what the eye sees.
 *   - the separator is a HAIRLINE ACROSS the strip. It is the one part whose
 *     default orientation is the opposite of the toolbar's, so a recipe that
 *     guessed the axis paints a 1px-tall nothing instead of a 1px-wide rule.
 *   - a disabled control is DIMMED. Its `data-[disabled]:opacity-50` arm fires off
 *     Base UI's data attribute, not `:disabled` — which is absent by default —
 *     so a recipe written against the pseudo-class silently paints nothing.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { italicDisabled: true },
  play: async ({ canvas }) => {
    const root = canvas.getByTestId("format");
    const rootStyle = getComputedStyle(root);
    await expect(rootStyle.display).toBe("flex");
    await expect(rootStyle.flexDirection).toBe("row");
    await expect(rootStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(parseFloat(rootStyle.borderTopWidth)).toBeGreaterThan(0);
    await expect(parseFloat(rootStyle.paddingTop)).toBeGreaterThan(0);

    // A vertical rule in a horizontal strip: thin across, tall along.
    const separator = canvas.getByTestId("format-separator");
    const rule = separator.getBoundingClientRect();
    await expect(Math.round(rule.width)).toBe(1);
    await expect(rule.height).toBeGreaterThan(1);
    await expect(getComputedStyle(separator).backgroundColor).not.toBe("rgba(0, 0, 0, 0)");

    // The controls sit on one line, at the same height.
    const bold = canvas.getByTestId("format-bold").getBoundingClientRect();
    const help = canvas.getByTestId("format-help").getBoundingClientRect();
    await expect(Math.round(bold.top)).toBe(Math.round(help.top));

    // `data-[disabled]:opacity-50` fired off the data attribute, not `:disabled`.
    const italic = canvas.getByTestId("format-italic");
    await expect(italic).toHaveAttribute("data-disabled");
    await expect(parseFloat(getComputedStyle(italic).opacity)).toBeLessThan(1);
    const boldStyle = getComputedStyle(canvas.getByTestId("format-bold"));
    await expect(parseFloat(boldStyle.opacity)).toBe(1);
  },
};
