import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactElement, ReactNode } from "react";
import { expect, fn, waitFor } from "storybook/test";

import { Avatar } from "./avatar";

/*
 * Wallow-m5aq.4.3 — Avatar stories. `@storybook/addon-vitest` turns each export
 * below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, but with the real Tailwind pipeline attached
 * (.storybook/preview.css), so this is the only place the circular frame, the
 * clipping and the muted fallback colour can actually be seen.
 *
 * Division of labour with avatar.test.tsx: that file proves WHICH part is
 * mounted, this one proves the mounted part is PAINTED (PaintedByTheDesignTokens
 * reads computed styles, which the `browser` project cannot do because it
 * compiles no Tailwind).
 *
 * Every `src` is a `data:` URI, so a story never waits on the network — see the
 * avatar.test.tsx header for the measured load/error semantics. Callback spies
 * come from `fn()` in `storybook/test` (never `vi.fn()`, which the Interactions
 * panel cannot display).
 */

/** A real 1x1 PNG, stretched over the frame by `object-cover`. */
const PHOTO =
  "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

/** Well-formed base64 that is not a PNG: the decode fails, so the load errors. */
const BROKEN_PHOTO = "data:image/png;base64,VGhpc0lzTm90QVBORw==";

interface UserAvatarProps {
  /** The photo to try. Omitted, the avatar is initials-only. */
  readonly src?: string;
  /** The initials shown while there is no decoded photo. */
  readonly initials?: ReactNode;
  /** Milliseconds to hold the fallback back, so a fast photo cannot flash it. */
  readonly delay?: number;
  /** Utilities merged over the root recipe. */
  readonly className?: string;
  /** Called with each loading status the image reports. */
  readonly onLoadingStatusChange?: (status: string) => void;
}

/** The story subject: a photo with initials behind it, as an app would write it. */
function UserAvatar({
  src,
  initials = "AL",
  delay,
  className,
  onLoadingStatusChange,
}: UserAvatarProps): ReactElement {
  return (
    <Avatar.Root className={className} data-testid="avatar">
      <Avatar.Image
        src={src}
        alt="Ada Lovelace"
        data-testid="avatar-image"
        onLoadingStatusChange={onLoadingStatusChange}
      />
      <Avatar.Fallback delay={delay} data-testid="avatar-fallback">
        {initials}
      </Avatar.Fallback>
    </Avatar.Root>
  );
}

const meta = {
  title: "Components/Avatar",
  component: UserAvatar,
  args: { onLoadingStatusChange: fn() },
} satisfies Meta<typeof UserAvatar>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The initials-only avatar — no `src` at all, the commonest state in an app. */
export const Initials: Story = {};

/** The photo, once the browser has decoded it. */
export const WithPhoto: Story = {
  args: { src: PHOTO },
};

/** A larger avatar, proving the root recipe's `size-10` is a caller override. */
export const Large: Story = {
  args: { src: PHOTO, className: "size-16" },
};

/** Longer initials still fit, because the fallback centres inside the frame. */
export const LongInitials: Story = {
  args: { initials: "ADA" },
};

/**
 * The fallback-on-error behaviour, driven for real: a `src` the browser cannot
 * decode leaves the initials mounted and never mounts an `<img>`.
 */
export const BrokenPhoto: Story = {
  args: { src: BROKEN_PHOTO },
  play: async ({ args, canvas }) => {
    await waitFor(async () => {
      await expect(args.onLoadingStatusChange).toHaveBeenCalledWith("error");
    });

    await expect(canvas.getByTestId("avatar-fallback")).toHaveTextContent("AL");
    await expect(canvas.queryByTestId("avatar-image")).not.toBeInTheDocument();
  },
};

/**
 * A delayed fallback: the frame is empty for the first 150ms so an avatar whose
 * photo arrives quickly never flashes its initials first.
 */
export const DelayedFallback: Story = {
  args: { delay: 150 },
  play: async ({ canvas }) => {
    await expect(canvas.queryByTestId("avatar-fallback")).not.toBeInTheDocument();

    await waitFor(async () => {
      await expect(canvas.getByTestId("avatar-fallback")).toBeInTheDocument();
    });
  },
};

/**
 * The proof the recipes PAINT. This is the assertion the `browser` project
 * cannot make: it compiles no Tailwind, so only here does `rounded-full`,
 * `overflow-hidden` and `bg-muted` become computed style. An empty stub recipe
 * fails every line below.
 */
export const PaintedByTheDesignTokens: Story = {
  play: async ({ canvas }) => {
    const rootStyle = getComputedStyle(canvas.getByTestId("avatar"));

    await expect(rootStyle.borderTopLeftRadius).not.toBe("0px");
    await expect(rootStyle.overflowX).toBe("hidden");
    await expect(rootStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(rootStyle.display).toBe("inline-flex");
    await expect(rootStyle.width).not.toBe("0px");

    // `text-sm font-medium text-muted-foreground` on the initials, against the
    // body size the reset leaves a bare <span> at.
    const fallbackStyle = getComputedStyle(canvas.getByTestId("avatar-fallback"));
    await expect(fallbackStyle.fontSize).not.toBe("16px");
    await expect(Number(fallbackStyle.fontWeight)).toBeGreaterThan(400);
    await expect(fallbackStyle.display).toBe("flex");
  },
};
