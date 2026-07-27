import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { Menu } from "../menu/menu";
import { Menubar } from "./menubar";
import { menubarTriggerRecipe } from "./menubar.styles";

/*
 * Wallow-m5aq.3.8 — Menubar stories. `@storybook/addon-vitest` turns every export
 * below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec while
 * menubar.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Three things belong HERE rather than in menubar.test.tsx:
 *
 *   - POINTER interaction, including the one that defines a menubar: with a menu
 *     already down, MOVING THE POINTER onto a neighbouring name switches menus
 *     without a second click. Base UI always renders a fixed, full-window pointer
 *     blocker inside the portal, and `vitest/browser`'s `userEvent` drives real
 *     Playwright input that hit-tests the click point and hits the blocker
 *     instead. `storybook/test`'s `userEvent` is `@testing-library/user-event`,
 *     which dispatches straight at the element with no hit-testing — and here real
 *     Tailwind is loaded, so the popup's `z-50` clears the blocker anyway.
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens and Vertical) — this project compiles real Tailwind
 *     and the `browser` project does not. The bar's orientation styling is a pair
 *     of `data-[orientation=…]:` modifiers, which only a real stylesheet can prove.
 *
 * A NOTE ON `toBeVisible()`: the menu popup recipe carries a 150ms enter transition
 * that starts at `opacity-0`, so a popup is not "visible" for the duration of that
 * transition. Every visibility assertion below is wrapped in `waitFor` — asserting
 * it synchronously right after opening is the failure the Dialog exemplar hit and
 * pinned for the whole wave.
 *
 * WHY THE MENUS LOOK EXACTLY LIKE THE MENU COMPONENT'S: they ARE the Menu
 * component's. Base UI's menubar subpath publishes only the strip, so everything
 * below a name here is `Menu`'s own wrapper carrying `menu.styles.ts`'s recipes,
 * and these stories are as much a regression pin on that reuse as they are their
 * own coverage.
 */

interface AppMenubarProps {
  /** Lays the bar out as a vertical rail instead of a horizontal strip. */
  readonly orientation?: "horizontal" | "vertical";
  /** Disables every menu on the bar at once. */
  readonly disabled?: boolean;
  /** Opens the File menu on first render, for the screenshot stories. */
  readonly defaultOpen?: boolean;
  /** Renders the checkbox row and the radio group in the View menu. */
  readonly withSelection?: boolean;
  /** Called with the File menu's new open state. */
  readonly onOpenChange?: (open: boolean) => void;
}

/**
 * The application bar almost every desktop-shaped product has: File, Edit, View.
 * Each name is an ordinary `Menu.Root` — the bar publishes no dropdown parts of
 * its own — and each trigger takes `menubarTriggerRecipe()` for the padding and
 * open-state highlight a name standing in a strip wants.
 */
function AppMenubar({
  orientation,
  disabled,
  defaultOpen,
  withSelection,
  onOpenChange,
}: AppMenubarProps): ReactElement {
  return (
    <Menubar data-testid="bar" orientation={orientation} disabled={disabled}>
      <Menu.Root defaultOpen={defaultOpen} onOpenChange={onOpenChange}>
        <Menu.Trigger data-testid="file-trigger" className={menubarTriggerRecipe()}>
          File
        </Menu.Trigger>
        <Menu.Portal>
          <Menu.Positioner data-testid="file-positioner">
            <Menu.Popup data-testid="file-popup">
              <Menu.Group>
                <Menu.GroupLabel data-testid="file-group-label">Project</Menu.GroupLabel>
                <Menu.Item data-testid="file-new">New file</Menu.Item>
                <Menu.Item data-testid="file-open">Open…</Menu.Item>
              </Menu.Group>
              <Menu.Separator data-testid="file-separator" />
              <Menu.SubmenuRoot>
                <Menu.SubmenuTrigger data-testid="file-recent">Recent</Menu.SubmenuTrigger>
                <Menu.Portal>
                  <Menu.Positioner>
                    <Menu.Popup data-testid="file-recent-popup">
                      <Menu.Item data-testid="file-recent-one">wallow.md</Menu.Item>
                      <Menu.Item data-testid="file-recent-two">branding.json</Menu.Item>
                    </Menu.Popup>
                  </Menu.Positioner>
                </Menu.Portal>
              </Menu.SubmenuRoot>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>
      <Menu.Root>
        <Menu.Trigger data-testid="edit-trigger" className={menubarTriggerRecipe()}>
          Edit
        </Menu.Trigger>
        <Menu.Portal>
          <Menu.Positioner>
            <Menu.Popup data-testid="edit-popup">
              <Menu.Item data-testid="edit-undo">Undo</Menu.Item>
              <Menu.Item data-testid="edit-redo" disabled>
                Redo
              </Menu.Item>
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>
      <Menu.Root>
        <Menu.Trigger data-testid="view-trigger" className={menubarTriggerRecipe()}>
          View
        </Menu.Trigger>
        <Menu.Portal>
          <Menu.Positioner>
            <Menu.Popup data-testid="view-popup">
              {withSelection ? (
                <>
                  <Menu.CheckboxItem data-testid="view-grid">
                    <Menu.CheckboxItemIndicator data-testid="view-grid-indicator">
                      ✓
                    </Menu.CheckboxItemIndicator>
                    Show grid
                  </Menu.CheckboxItem>
                  <Menu.Separator />
                  <Menu.RadioGroup data-testid="view-density" defaultValue="cosy">
                    <Menu.RadioItem data-testid="view-density-cosy" value="cosy">
                      <Menu.RadioItemIndicator data-testid="view-density-cosy-indicator">
                        •
                      </Menu.RadioItemIndicator>
                      Cosy
                    </Menu.RadioItem>
                    <Menu.RadioItem data-testid="view-density-compact" value="compact">
                      <Menu.RadioItemIndicator>•</Menu.RadioItemIndicator>
                      Compact
                    </Menu.RadioItem>
                  </Menu.RadioGroup>
                </>
              ) : (
                <Menu.Item data-testid="view-zoom">Zoom in</Menu.Item>
              )}
            </Menu.Popup>
          </Menu.Positioner>
        </Menu.Portal>
      </Menu.Root>
    </Menubar>
  );
}

const meta = {
  title: "Components/Menubar",
  component: AppMenubar,
  args: {
    onOpenChange: fn(),
  },
} satisfies Meta<typeof AppMenubar>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The bar at rest: three names on one strip, hugging their own width. */
export const Default: Story = {};

/** One menu down, so the strip, the highlighted name and the card are all visible at once. */
export const OpenMenu: Story = {
  args: { defaultOpen: true },
};

/** The state-carrying rows a View menu usually holds, opened from the bar. */
export const WithSelectionItems: Story = {
  args: { withSelection: true },
  play: async ({ canvas }) => {
    await userEvent.click(canvas.getByTestId("view-trigger"));

    const popup = await screen.findByTestId("view-popup");
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(screen.getByTestId("view-density-cosy")).toHaveAttribute("aria-checked", "true");
    await expect(screen.getByTestId("view-density-cosy-indicator")).toBeVisible();
  },
};

/**
 * The interaction that makes a bar a bar: once a menu is down, moving the pointer
 * onto a neighbouring name switches menus with no second click. Reaching for this
 * with `vitest/browser`'s Playwright-driven `userEvent` is what the pointer blocker
 * makes impossible, so it lives here.
 */
export const SwitchesMenusOnHover: Story = {
  play: async ({ args, canvas }) => {
    const file = canvas.getByTestId("file-trigger");

    await userEvent.click(file);

    const filePopup = await screen.findByTestId("file-popup");
    await waitFor(async () => {
      await expect(filePopup).toBeVisible();
    });
    await expect(file).toHaveAttribute("data-popup-open");
    await expect(canvas.getByTestId("bar")).toHaveAttribute("data-has-submenu-open");
    await expect(args.onOpenChange).toHaveBeenCalledWith(true, expect.anything());

    await userEvent.hover(canvas.getByTestId("edit-trigger"));

    await waitFor(async () => {
      await expect(screen.queryByTestId("file-popup")).not.toBeInTheDocument();
    });
    await expect(await screen.findByTestId("edit-popup")).toHaveAttribute("data-open");
    await expect(canvas.getByTestId("edit-trigger")).toHaveAttribute("data-popup-open");
    await expect(file).not.toHaveAttribute("data-popup-open");
  },
};

/**
 * The keyboard path end to end: the bar is ONE tab stop, the arrows walk the names,
 * down opens the menu under the name, and a nested row opens to the side.
 */
export const KeyboardWalksTheBar: Story = {
  play: async ({ canvas }) => {
    const file = canvas.getByTestId("file-trigger");
    file.focus();

    await userEvent.keyboard("{ArrowRight}");
    await waitFor(async () => {
      await expect(canvas.getByTestId("edit-trigger")).toHaveFocus();
    });
    // Roving, not three tab stops: only the focused name is tabbable.
    await expect(file).toHaveAttribute("tabindex", "-1");

    await userEvent.keyboard("{ArrowLeft}{ArrowDown}");
    const popup = await screen.findByTestId("file-popup");
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(screen.getByTestId("file-new")).toHaveFocus();

    await userEvent.keyboard("{End}{ArrowRight}");
    await waitFor(async () => {
      await expect(screen.getByTestId("file-recent-one")).toHaveFocus();
    });
    await expect(screen.getByTestId("file-recent-popup")).toHaveAttribute("data-nested");

    await userEvent.keyboard("{Escape}{Escape}");
    await waitFor(async () => {
      await expect(screen.queryByTestId("file-popup")).not.toBeInTheDocument();
    });
    await expect(file).toHaveFocus();
  },
};

/**
 * The vertical rail — the same class list as the horizontal bar, because the
 * orientation styling hangs off Base UI's own `data-orientation` attribute. The
 * play function reads the computed direction, which is the only way to prove those
 * modifiers resolve to real Tailwind utilities.
 */
export const Vertical: Story = {
  args: { orientation: "vertical" },
  play: async ({ canvas }) => {
    const bar = canvas.getByTestId("bar");

    await expect(bar).toHaveAttribute("data-orientation", "vertical");
    await expect(getComputedStyle(bar).flexDirection).toBe("column");

    // In a vertical bar the arrow axis turns with it: down walks the names rather
    // than opening the menu under one.
    canvas.getByTestId("file-trigger").focus();
    await userEvent.keyboard("{ArrowDown}");
    await waitFor(async () => {
      await expect(canvas.getByTestId("edit-trigger")).toHaveFocus();
    });
    await expect(screen.queryByTestId("file-popup")).not.toBeInTheDocument();
  },
};

/** Every menu on the bar disabled by one prop on the strip. */
export const Disabled: Story = {
  args: { disabled: true },
  play: async ({ canvas }) => {
    const file = canvas.getByTestId("file-trigger");

    await expect(file).toBeDisabled();
    await expect(file).toHaveAttribute("data-disabled");

    await userEvent.click(file);

    await expect(screen.queryByTestId("file-popup")).not.toBeInTheDocument();
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to a
 * `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented class
 * names passes menubar.test.tsx's class-set assertions and still paints nothing.
 * These assertions read computed styles instead of class names.
 *
 * The strip's and the trigger delta's are this component's own; everything below a
 * name is inherited from the Menu component's recipes and pinned here so a swap
 * away from those shared wrappers cannot go unnoticed.
 */
export const PaintedByTheDesignTokens: Story = {
  play: async ({ canvas }) => {
    // `flex`, `data-[orientation=horizontal]:flex-row`, `gap-1`, `rounded-md`,
    // `border border-border`, `bg-background`, `p-1` and `shadow-sm` on the strip.
    // `flex-row` comes from a data-attribute modifier, so it is the utility most
    // likely to be silently absent.
    const bar = canvas.getByTestId("bar");
    const barStyle = getComputedStyle(bar);
    await expect(barStyle.display).toBe("flex");
    await expect(barStyle.flexDirection).toBe("row");
    await expect(barStyle.alignItems).toBe("center");
    await expect(barStyle.columnGap).not.toBe("normal");
    await expect(barStyle.borderTopWidth).not.toBe("0px");
    await expect(barStyle.borderTopLeftRadius).not.toBe("0px");
    await expect(barStyle.paddingTop).not.toBe("0px");
    await expect(barStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(barStyle.boxShadow).not.toBe("none");

    // `w-fit`: the bar hugs its three names rather than stretching across the page.
    await expect(bar.getBoundingClientRect().width).toBeLessThan(
      document.documentElement.clientWidth,
    );

    // `px-3 py-1.5` from the trigger delta, on top of the `text-sm`/`inline-flex`
    // shape `menuTriggerRecipe` supplies.
    //
    // The computed display reads `flex`, not `inline-flex`: a menubar trigger is a
    // flex item of the strip, and CSS blockifies a flex item's display. The
    // assertion still discriminates — a trigger that lost `menuTriggerRecipe` would
    // be a bare `<button>`, whose `inline-block` blockifies to `block`.
    const trigger = canvas.getByTestId("file-trigger");
    const closedTriggerStyle = getComputedStyle(trigger);
    await expect(closedTriggerStyle.paddingLeft).not.toBe("0px");
    await expect(closedTriggerStyle.paddingTop).not.toBe("0px");
    await expect(closedTriggerStyle.display).toBe("flex");
    const restingBackground = closedTriggerStyle.backgroundColor;

    await userEvent.click(trigger);

    const popup = await screen.findByTestId("file-popup");
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });

    // `data-[popup-open]:bg-accent`: the open menu's name is filled, so the card on
    // screen visibly belongs to it.
    await expect(getComputedStyle(trigger).backgroundColor).not.toBe(restingBackground);

    // Inherited from the Menu recipes below here — `z-50` on the positioner, and
    // `min-w-32 rounded-md border bg-popover p-1` on the card.
    await expect(getComputedStyle(screen.getByTestId("file-positioner")).zIndex).toBe("50");

    const popupStyle = getComputedStyle(popup);
    await expect(popupStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(popupStyle.borderTopWidth).not.toBe("0px");
    await expect(popupStyle.paddingTop).not.toBe("0px");
    await expect(popupStyle.minWidth).not.toBe("0px");

    // `px-2 py-1.5 text-sm` on a row, `text-xs text-muted-foreground` on the label
    // above it, and `h-px bg-border` on the rule between the groups.
    const rowStyle = getComputedStyle(screen.getByTestId("file-new"));
    await expect(rowStyle.paddingLeft).not.toBe("0px");

    const labelStyle = getComputedStyle(screen.getByTestId("file-group-label"));
    await expect(Number.parseFloat(labelStyle.fontSize)).toBeLessThan(
      Number.parseFloat(rowStyle.fontSize),
    );

    const separatorStyle = getComputedStyle(screen.getByTestId("file-separator"));
    await expect(separatorStyle.height).toBe("1px");
    await expect(separatorStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
  },
};

/**
 * The controlled shape: one menu's open state lives in the caller's `useState`, so
 * a command elsewhere in the app can drop the File menu down. The bar keeps its own
 * behaviour — switching menus, roving focus — around the controlled one.
 */
export const Controlled: Story = {
  render: function ControlledMenubar(args) {
    const [open, setOpen] = useState(false);

    return (
      <div className="flex flex-col gap-2">
        <button
          type="button"
          data-testid="controlled-external"
          className="w-fit text-sm text-foreground"
          onClick={() => setOpen(true)}
        >
          Open the File menu from outside the bar
        </button>
        <span data-testid="controlled-state">{open ? "open" : "closed"}</span>
        <Menubar data-testid="controlled-bar">
          <Menu.Root
            open={open}
            onOpenChange={(next) => {
              setOpen(next);
              args.onOpenChange?.(next);
            }}
          >
            <Menu.Trigger data-testid="controlled-file" className={menubarTriggerRecipe()}>
              File
            </Menu.Trigger>
            <Menu.Portal>
              <Menu.Positioner>
                <Menu.Popup data-testid="controlled-popup">
                  <Menu.Item data-testid="controlled-new">New file</Menu.Item>
                </Menu.Popup>
              </Menu.Positioner>
            </Menu.Portal>
          </Menu.Root>
          <Menu.Root>
            <Menu.Trigger data-testid="controlled-edit" className={menubarTriggerRecipe()}>
              Edit
            </Menu.Trigger>
            <Menu.Portal>
              <Menu.Positioner>
                <Menu.Popup data-testid="controlled-edit-popup">
                  <Menu.Item data-testid="controlled-undo">Undo</Menu.Item>
                </Menu.Popup>
              </Menu.Positioner>
            </Menu.Portal>
          </Menu.Root>
        </Menubar>
      </div>
    );
  },
  play: async ({ canvas }) => {
    await userEvent.click(canvas.getByTestId("controlled-external"));

    const popup = await screen.findByTestId("controlled-popup");
    await expect(popup).toHaveAttribute("data-open");
    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("open");

    await userEvent.click(screen.getByTestId("controlled-new"));

    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("closed");
    await waitFor(async () => {
      await expect(screen.queryByTestId("controlled-popup")).not.toBeInTheDocument();
    });
  },
};
