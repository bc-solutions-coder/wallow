import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement } from "react";
import { expect, waitFor } from "storybook/test";

import { ScrollArea } from "./scroll-area";

/*
 * Wallow-m5aq.4.5 — Scroll Area stories. `@storybook/addon-vitest` turns every
 * export below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while scroll-area.test.tsx holds the markup assertions a screenshot cannot
 * make.
 *
 * Nothing about Scroll Area is portalled, so every play function queries `canvas`.
 *
 * The subject fixes the area's outer size in the story rather than leaving it to
 * a parent, because a scroll area with no bounded height has nothing to scroll:
 * the whole component only exists once the content is taller than the window
 * onto it.
 */

interface ChangelogAreaProps {
  /** Height of the window onto the content. */
  readonly height?: number;
  /** Width of the window onto the content. */
  readonly width?: number;
  /** How many paragraphs to render inside — more than fits is the point. */
  readonly entryCount?: number;
  /** Renders one very wide line, so the horizontal track appears too. */
  readonly wide?: boolean;
}

/**
 * A realistic bounded panel — the story subject. Stories drive the real
 * `ScrollArea` namespace through this so every part is exercised together rather
 * than one part at a time.
 */
function ChangelogArea({
  height = 160,
  width = 280,
  entryCount = 8,
  wide = false,
}: ChangelogAreaProps): ReactElement {
  return (
    <ScrollArea.Root data-testid="changelog" style={{ height, width }}>
      <ScrollArea.Viewport data-testid="changelog-viewport">
        <ScrollArea.Content data-testid="changelog-content">
          {wide ? (
            <p data-testid="changelog-wide" style={{ whiteSpace: "nowrap" }}>
              A single line that is far wider than the window onto it, so the horizontal track has
              something to scroll.
            </p>
          ) : null}
          {Array.from({ length: entryCount }, (_, index) => (
            <p key={index} data-testid={`changelog-entry-${index}`}>
              Release {index + 1} — assorted fixes and one new component.
            </p>
          ))}
        </ScrollArea.Content>
      </ScrollArea.Viewport>
      <ScrollArea.Scrollbar data-testid="changelog-track-y" orientation="vertical">
        <ScrollArea.Thumb data-testid="changelog-thumb-y" />
      </ScrollArea.Scrollbar>
      <ScrollArea.Scrollbar data-testid="changelog-track-x" orientation="horizontal">
        <ScrollArea.Thumb data-testid="changelog-thumb-x" />
      </ScrollArea.Scrollbar>
      <ScrollArea.Corner data-testid="changelog-corner" />
    </ScrollArea.Root>
  );
}

const meta = {
  title: "Components/ScrollArea",
  component: ChangelogArea,
} satisfies Meta<typeof ChangelogArea>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The everyday state: more content than fits, so the vertical track appears. */
export const Default: Story = {};

/** Both axes overflow, so both tracks AND the corner between them are painted. */
export const BothAxes: Story = {
  args: { wide: true, width: 220 },
};

/** Nothing to scroll: Base UI mounts no track at all, and the recipe paints nothing. */
export const NoOverflow: Story = {
  args: { entryCount: 1, height: 200 },
  play: async ({ canvas }) => {
    await expect(canvas.queryByTestId("changelog-track-y")).toBeNull();
    await expect(canvas.queryByTestId("changelog-track-x")).toBeNull();
  },
};

/**
 * The interaction half: scrolling the viewport moves the thumb. The wheel event
 * comes first because Base UI treats an unannounced scroll as programmatic and
 * only a user-driven one publishes `data-scrolling`.
 */
export const ScrollingMovesTheThumb: Story = {
  play: async ({ canvas }) => {
    const viewport = canvas.getByTestId("changelog-viewport");
    const thumb = await waitFor(() => canvas.getByTestId("changelog-thumb-y"));
    const before = thumb.style.transform;

    viewport.dispatchEvent(new WheelEvent("wheel", { deltaY: 80, bubbles: true }));
    viewport.scrollTo({ top: 80 });

    await waitFor(() => {
      expect(canvas.getByTestId("changelog-thumb-y").style.transform).not.toBe(before);
    });
    await waitFor(() => {
      expect(canvas.getByTestId("changelog-viewport")).toHaveAttribute("data-scrolling");
    });
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of well-formed
 * but non-existent utility names passes every class-set assertion in
 * scroll-area.test.tsx and still paints nothing.
 *
 * Three of these assertions cannot be made anywhere else in the suite:
 *   - the native scrollbars must stay hidden. Base UI's own
 *     `base-ui-disable-scrollbar` class is what does that, and it only works if
 *     the viewport recipe was merged ON TOP of it rather than replacing it —
 *     which shows up as the viewport's client width equalling its offset width.
 *   - the thumb must be NARROWER than nothing and TALLER than nothing: its
 *     cross-axis size comes from the recipe (`w-full`) and its along-axis size
 *     from Base UI's custom property, so a missing `w-full` collapses it to zero.
 *   - the track has to be a visible, token-coloured strip rather than a
 *     transparent hit area.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { wide: true, width: 220 },
  play: async ({ canvas }) => {
    const root = canvas.getByTestId("changelog");
    const rootStyle = getComputedStyle(root);
    await expect(rootStyle.position).toBe("relative");
    await expect(rootStyle.overflow).toBe("hidden");
    await expect(rootStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");

    // `base-ui-disable-scrollbar` survived the merge: no native gutter is eating
    // into the viewport's inner width.
    const viewport = canvas.getByTestId("changelog-viewport");
    await expect(viewport.clientWidth).toBe(viewport.offsetWidth);

    const track = await waitFor(() => canvas.getByTestId("changelog-track-y"));
    const trackStyle = getComputedStyle(track);
    await expect(trackStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(Math.round(parseFloat(trackStyle.width))).toBe(8);

    const thumb = canvas.getByTestId("changelog-thumb-y");
    const thumbStyle = getComputedStyle(thumb);
    await expect(thumbStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(Math.round(parseFloat(thumbStyle.width))).toBe(8);
    await expect(parseFloat(thumbStyle.height)).toBeGreaterThan(0);

    const horizontalThumb = canvas.getByTestId("changelog-thumb-x");
    await expect(Math.round(parseFloat(getComputedStyle(horizontalThumb).height))).toBe(8);
  },
};
