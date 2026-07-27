import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { Drawer } from "./drawer";
import type { DrawerSide } from "./drawer.styles";

/*
 * Wallow-m5aq.3.10 — Drawer stories. `@storybook/addon-vitest` turns every
 * export below into a Vitest test case rendered in the same headless Chromium
 * the `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while drawer.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Three things belong HERE rather than in drawer.test.tsx:
 *
 *   - POINTER interaction inside the open popup. Base UI always renders a fixed,
 *     full-window pointer blocker inside the portal, and `vitest/browser`'s
 *     `userEvent` drives real Playwright input, which hit-tests the click point
 *     and therefore hits the blocker instead of the popup. `userEvent` here is
 *     `@testing-library/user-event` (bundled by `storybook/test`), which
 *     dispatches synthetic events straight at the element with no hit-testing,
 *     so clicking a part inside an open popup just works.
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens) — this project compiles real Tailwind and the
 *     `browser` project does not. For a drawer that is most of the component:
 *     `side` is a positioning variant, and only a project with real CSS can show
 *     that `items-end` and `border-l` reached the right edge.
 *   - TAILWIND v4 EMITS `translate-*` AND `scale-*` AS THE INDIVIDUAL `translate`
 *     AND `scale` CSS PROPERTIES, NOT AS A `transform` FUNCTION. Nothing in this
 *     file may assert `getComputedStyle(...).transform` for a drawer's slide or
 *     the indent's scale-back — it is legitimately "none" forever, and both
 *     stories below pin that explicitly so the next reader does not "fix" it.
 */

/** Base UI's `swipeDirection` for each `side` — it names the direction the
 * drawer is swiped AWAY in, so a caller sets both and they must stay in step. */
const SWIPE_DIRECTION_FOR_SIDE: Record<DrawerSide, "down" | "left" | "right" | "up"> = {
  bottom: "down",
  left: "left",
  right: "right",
  top: "up",
};

interface FilterDrawerProps {
  /** The screen edge the drawer is anchored to. */
  readonly side?: DrawerSide;
  /** Opens the drawer on first render, for the screenshot stories. */
  readonly defaultOpen?: boolean;
  /** Renders form fields inside the popup, under a virtual keyboard provider. */
  readonly withForm?: boolean;
  /** Called with the drawer's new open state. */
  readonly onOpenChange?: (open: boolean) => void;
}

/**
 * A complete, realistic drawer — the story subject. Stories drive the real
 * `Drawer` namespace through this so every part is exercised together rather
 * than one part at a time: the swipe area on the same edge, the indent effect
 * scaling the page behind it, and the whole portalled panel.
 */
function FilterDrawer({
  side = "bottom",
  defaultOpen,
  withForm,
  onOpenChange,
}: FilterDrawerProps): ReactElement {
  const panel = (
    <Drawer.Portal>
      <Drawer.Backdrop data-testid="filter-backdrop" />
      <Drawer.Viewport data-testid="filter-viewport" side={side}>
        <Drawer.Popup data-testid="filter-popup" side={side}>
          <Drawer.Content data-testid="filter-content">
            <Drawer.Title data-testid="filter-title">Filter results</Drawer.Title>
            <Drawer.Description data-testid="filter-description">
              Narrow the project list down to what you are looking for.
            </Drawer.Description>
            {withForm ? (
              <form className="mt-4 flex flex-col gap-2">
                <label className="text-sm text-foreground" htmlFor="filter-owner">
                  Owner
                </label>
                <input
                  id="filter-owner"
                  data-testid="filter-owner"
                  className="rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground"
                />
              </form>
            ) : null}
            <div className="mt-6 flex justify-end gap-2">
              <Drawer.Close data-testid="filter-cancel">Cancel</Drawer.Close>
            </div>
          </Drawer.Content>
        </Drawer.Popup>
      </Drawer.Viewport>
    </Drawer.Portal>
  );

  return (
    <Drawer.Provider>
      <Drawer.IndentBackground data-testid="filter-indent-background" />
      <Drawer.Indent data-testid="filter-indent">
        <div className="min-h-40 rounded-lg bg-background p-6 text-foreground">
          <p className="mb-4 text-sm text-muted-foreground">
            The page behind the drawer. It scales back while the drawer is open.
          </p>
          <Drawer.Root
            defaultOpen={defaultOpen}
            onOpenChange={onOpenChange}
            swipeDirection={SWIPE_DIRECTION_FOR_SIDE[side]}
          >
            <Drawer.Trigger data-testid="filter-trigger">Filter results</Drawer.Trigger>
            <Drawer.SwipeArea data-testid="filter-swipe-area" side={side} />
            {/* The virtual keyboard provider MUST sit inside Drawer.Root — it
                reads the root's store and throws outside one. */}
            {withForm ? (
              <Drawer.VirtualKeyboardProvider>{panel}</Drawer.VirtualKeyboardProvider>
            ) : (
              panel
            )}
          </Drawer.Root>
        </div>
      </Drawer.Indent>
    </Drawer.Provider>
  );
}

const meta = {
  title: "Components/Drawer",
  component: FilterDrawer,
  args: {
    onOpenChange: fn(),
  },
} satisfies Meta<typeof FilterDrawer>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The closed trigger — the state a page shows most of the time. */
export const Default: Story = {};

/** The open bottom sheet: backdrop, panel on the bottom edge, title and close. */
export const Open: Story = {
  args: { defaultOpen: true },
};

/** Anchored to the right edge — the classic settings/filters side panel. */
export const SideRight: Story = {
  args: { defaultOpen: true, side: "right" },
};

/** Anchored to the left edge, the mirror of SideRight (radius and border flip). */
export const SideLeft: Story = {
  args: { defaultOpen: true, side: "left" },
};

/** Anchored to the top edge — a notification/command sheet. */
export const SideTop: Story = {
  args: { defaultOpen: true, side: "top" },
};

/**
 * A drawer whose body is a form, wrapped in `Drawer.VirtualKeyboardProvider` so
 * a software keyboard resizes the sheet instead of covering the field. This is
 * the shape a consumer copies for any drawer containing inputs.
 */
export const WithForm: Story = {
  args: { defaultOpen: true, withForm: true },
};

/**
 * The controlled shape: the open state lives in the caller's `useState`, and the
 * drawer reports every change back through `onOpenChange`. This is the story a
 * consumer copies when the drawer has to open from somewhere other than its own
 * trigger.
 */
export const Controlled: Story = {
  render: function ControlledDrawer(args) {
    const [open, setOpen] = useState(false);

    return (
      <div className="flex flex-col gap-2">
        <button
          type="button"
          data-testid="controlled-external"
          className="text-sm text-foreground"
          onClick={() => setOpen(true)}
        >
          Open from outside the drawer
        </button>
        <span data-testid="controlled-state">{open ? "open" : "closed"}</span>
        <Drawer.Root
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            args.onOpenChange?.(next);
          }}
        >
          <Drawer.Portal>
            <Drawer.Backdrop data-testid="controlled-backdrop" />
            <Drawer.Viewport>
              <Drawer.Popup data-testid="controlled-popup">
                <Drawer.Content>
                  <Drawer.Title>Controlled</Drawer.Title>
                  <Drawer.Description>The caller owns the open state.</Drawer.Description>
                  <Drawer.Close data-testid="controlled-close">Close</Drawer.Close>
                </Drawer.Content>
              </Drawer.Popup>
            </Drawer.Viewport>
          </Drawer.Portal>
        </Drawer.Root>
      </div>
    );
  },
  play: async ({ canvas }) => {
    await userEvent.click(canvas.getByTestId("controlled-external"));

    const popup = await screen.findByTestId("controlled-popup");
    await expect(popup).toHaveAttribute("data-open");
    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("open");

    await userEvent.click(screen.getByTestId("controlled-close"));

    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("closed");
    await waitFor(async () => {
      await expect(screen.queryByTestId("controlled-popup")).not.toBeInTheDocument();
    });
  },
};

/** The interaction half: opening from the trigger, then dismissing. */
export const OpenAndDismiss: Story = {
  play: async ({ args, canvas }) => {
    const trigger = canvas.getByTestId("filter-trigger");

    await userEvent.click(trigger);

    // The popup is portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("filter-popup");
    // The popup recipe carries a 300ms enter transition that starts translated
    // fully off the edge, so it is not "visible" until that settles.
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(popup).toHaveAttribute("data-open");
    await expect(trigger).toHaveAttribute("data-popup-open");
    await expect(args.onOpenChange).toHaveBeenCalledWith(true, expect.anything());

    // Real Tailwind is loaded here, so the popup's `z-50` puts it above Base
    // UI's own pointer blocker and a genuine click lands. The `browser` project
    // cannot do this — see the header.
    await userEvent.click(screen.getByTestId("filter-cancel"));

    await waitFor(async () => {
      await expect(screen.queryByTestId("filter-popup")).not.toBeInTheDocument();
    });
    await expect(trigger).not.toHaveAttribute("data-popup-open");
  },
};

/** Pressing the backdrop dismisses the drawer — the other pointer path. */
export const DismissOnBackdropPress: Story = {
  args: { defaultOpen: true },
  play: async () => {
    const backdrop = await screen.findByTestId("filter-backdrop");

    await userEvent.click(backdrop);

    await waitFor(async () => {
      await expect(screen.queryByTestId("filter-popup")).not.toBeInTheDocument();
    });
  },
};

/**
 * The indent effect: while the drawer is open, the app UI behind it scales back
 * and the layer underneath fades in. Base UI supplies only the `data-active`
 * attribute — the whole visual is `data-[active]:scale-[0.97]` and
 * `data-[active]:opacity-100`, so it can only be proven where Tailwind is real.
 */
export const IndentEffect: Story = {
  play: async ({ canvas }) => {
    const indent = canvas.getByTestId("filter-indent");
    const indentBackground = canvas.getByTestId("filter-indent-background");

    await expect(indent).toHaveAttribute("data-inactive");
    await expect(getComputedStyle(indent).scale).toBe("none");

    await userEvent.click(canvas.getByTestId("filter-trigger"));

    await expect(indent).toHaveAttribute("data-active");
    await expect(indentBackground).toHaveAttribute("data-active");

    // Both properties settle over a 300ms transition, so they are polled.
    await waitFor(async () => {
      await expect(Number(getComputedStyle(indent).scale)).toBeLessThan(1);
    });
    await waitFor(async () => {
      await expect(Number(getComputedStyle(indentBackground).opacity)).toBeGreaterThan(0);
    });

    // TAILWIND v4: `scale-[0.97]` sets the individual `scale` property. The
    // `transform` property stays "none" — asserting it would be the bug.
    await expect(getComputedStyle(indent).transform).toBe("none");
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes drawer.test.tsx's class-set assertions and still paints
 * nothing. These assertions read computed styles instead of class names.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { defaultOpen: true, side: "right" },
  play: async () => {
    const backdrop = await screen.findByTestId("filter-backdrop");
    const viewport = await screen.findByTestId("filter-viewport");
    const popup = await screen.findByTestId("filter-popup");

    // `fixed inset-0 bg-foreground/50` on the backdrop, against the unstyled
    // defaults of `static` and a transparent background.
    await expect(getComputedStyle(backdrop).position).toBe("fixed");
    await expect(getComputedStyle(backdrop).backgroundColor).not.toBe("rgba(0, 0, 0, 0)");

    // `fixed inset-0 flex` plus the side's alignment on the viewport — this is
    // what actually puts the panel on the right edge, so it is asserted as the
    // resolved flex value rather than as a class name.
    const viewportStyle = getComputedStyle(viewport);
    await expect(viewportStyle.position).toBe("fixed");
    await expect(viewportStyle.display).toBe("flex");
    await expect(viewportStyle.justifyContent).toBe("flex-end");
    await expect(viewportStyle.alignItems).toBe("stretch");

    // `relative`, `w-80`, `bg-popover`, `border-l border-border`, `shadow-lg`
    // and `rounded-l-lg` on the panel itself. It is deliberately NOT `fixed`:
    // the viewport is the fixed layer.
    const popupStyle = getComputedStyle(popup);
    await expect(popupStyle.position).toBe("relative");
    await expect(popupStyle.width).toBe("320px");
    await expect(popupStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(popupStyle.borderLeftWidth).not.toBe("0px");
    await expect(popupStyle.borderLeftStyle).toBe("solid");
    await expect(popupStyle.borderTopLeftRadius).not.toBe("0px");
    await expect(popupStyle.borderTopRightRadius).toBe("0px");
    await expect(popupStyle.boxShadow).not.toBe("none");

    // TAILWIND v4, THE GOTCHA THIS COMPONENT LIVES ON: the panel's slide is
    // `translate-x-*`, which compiles to the individual `translate` property.
    // `transform` is "none" and must never be asserted as anything else;
    // `transition-transform` still covers the slide because it expands to
    // `transition-property: transform, translate, scale, rotate`.
    await expect(popupStyle.transform).toBe("none");
    await expect(popupStyle.transitionProperty).toContain("translate");

    // `p-6` on the content, which the popup deliberately does not carry.
    await expect(getComputedStyle(popup).paddingTop).toBe("0px");
    await expect(getComputedStyle(await screen.findByTestId("filter-content")).paddingTop).not.toBe(
      "0px",
    );

    // `text-lg font-semibold` on the title, against the <h2> defaults the reset
    // flattens to the body size.
    const titleStyle = getComputedStyle(await screen.findByTestId("filter-title"));
    await expect(titleStyle.fontSize).not.toBe("16px");
    await expect(Number(titleStyle.fontWeight)).toBeGreaterThan(400);
  },
};
