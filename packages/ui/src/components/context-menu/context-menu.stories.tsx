import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { ContextMenu } from "./context-menu";

/*
 * Wallow-m5aq.3.7 — Context Menu stories. `@storybook/addon-vitest` turns every
 * export below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec while
 * context-menu.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Three things belong HERE rather than in context-menu.test.tsx:
 *
 *   - THE RIGHT CLICK ITSELF, driven the way a user drives it. `userEvent` here is
 *     `@testing-library/user-event` (bundled by `storybook/test`), whose
 *     `pointer({ keys: "[MouseRight]" })` dispatches a real `contextmenu` event at
 *     the element.
 *   - POINTER interaction INSIDE the open popup. Base UI always renders a fixed,
 *     full-window pointer blocker inside the portal; `vitest/browser`'s `userEvent`
 *     drives real Playwright input, which hit-tests the click point and therefore
 *     hits the blocker instead of the row. `@testing-library/user-event` dispatches
 *     straight at the element with no hit-testing, so clicking a row just works —
 *     and here real Tailwind is loaded, so the popup's `z-50` clears the blocker
 *     anyway.
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens) — this project compiles real Tailwind and the
 *     `browser` project does not.
 *
 * A NOTE ON `toBeVisible()`: the popup recipe carries a 150ms enter transition that
 * starts at `opacity-0`, so the popup is not "visible" for the duration of that
 * transition. Every visibility assertion below is wrapped in `waitFor` — asserting
 * it synchronously right after opening is the failure the Dialog exemplar hit and
 * pinned for the whole wave.
 *
 * WHY THE POPUP LOOKS EXACTLY LIKE THE MENU COMPONENT'S: it IS the Menu component's.
 * Seventeen of the nineteen parts are Menu's own wrappers (see context-menu.tsx), so
 * everything below the trigger inherits Menu's recipes unchanged, and these stories
 * are as much a regression pin on that inheritance as they are their own coverage.
 */

interface ProjectCardMenuProps {
  /** Opens the menu on first render, for the screenshot stories. */
  readonly defaultOpen?: boolean;
  /** Renders the checkbox row and the radio group, so selection state is covered. */
  readonly withSelection?: boolean;
  /** Renders a nested submenu under its own trigger row. */
  readonly withSubmenu?: boolean;
  /** Called with the menu's new open state. */
  readonly onOpenChange?: (open: boolean) => void;
}

/**
 * A card that answers a right click — the story subject, and the shape almost every
 * real use of this component takes: the trigger is not a button next to the content,
 * it is the content.
 */
function ProjectCardMenu({
  defaultOpen,
  withSelection,
  withSubmenu,
  onOpenChange,
}: ProjectCardMenuProps): ReactElement {
  return (
    <ContextMenu.Root defaultOpen={defaultOpen} onOpenChange={onOpenChange}>
      <ContextMenu.Trigger
        data-testid="card-trigger"
        className="flex h-32 w-64 items-center justify-center border border-border bg-card p-4 text-sm text-card-foreground"
      >
        Right click this card
      </ContextMenu.Trigger>
      <ContextMenu.Portal>
        <ContextMenu.Backdrop data-testid="card-backdrop" />
        <ContextMenu.Positioner data-testid="card-positioner">
          <ContextMenu.Popup data-testid="card-popup">
            <ContextMenu.Group data-testid="card-group">
              <ContextMenu.GroupLabel data-testid="card-group-label">
                Project
              </ContextMenu.GroupLabel>
              <ContextMenu.Item data-testid="card-duplicate">Duplicate</ContextMenu.Item>
              <ContextMenu.Item data-testid="card-rename">Rename</ContextMenu.Item>
              <ContextMenu.LinkItem data-testid="card-docs" href="https://example.com/docs">
                Open documentation
              </ContextMenu.LinkItem>
            </ContextMenu.Group>
            {withSelection ? (
              <>
                <ContextMenu.Separator data-testid="card-separator" />
                <ContextMenu.CheckboxItem data-testid="card-grid">
                  <ContextMenu.CheckboxItemIndicator data-testid="card-grid-indicator">
                    ✓
                  </ContextMenu.CheckboxItemIndicator>
                  Show grid
                </ContextMenu.CheckboxItem>
                <ContextMenu.RadioGroup data-testid="card-density" defaultValue="cosy">
                  <ContextMenu.RadioItem data-testid="card-density-cosy" value="cosy">
                    <ContextMenu.RadioItemIndicator data-testid="card-density-cosy-indicator">
                      •
                    </ContextMenu.RadioItemIndicator>
                    Cosy
                  </ContextMenu.RadioItem>
                  <ContextMenu.RadioItem data-testid="card-density-compact" value="compact">
                    <ContextMenu.RadioItemIndicator data-testid="card-density-compact-indicator">
                      •
                    </ContextMenu.RadioItemIndicator>
                    Compact
                  </ContextMenu.RadioItem>
                </ContextMenu.RadioGroup>
              </>
            ) : null}
            {withSubmenu ? (
              <>
                <ContextMenu.Separator />
                <ContextMenu.SubmenuRoot>
                  <ContextMenu.SubmenuTrigger data-testid="card-move">
                    Move to
                  </ContextMenu.SubmenuTrigger>
                  <ContextMenu.Portal>
                    <ContextMenu.Positioner data-testid="card-move-positioner">
                      <ContextMenu.Popup data-testid="card-move-popup">
                        <ContextMenu.Item data-testid="card-move-archive">Archive</ContextMenu.Item>
                        <ContextMenu.Item data-testid="card-move-trash">Trash</ContextMenu.Item>
                      </ContextMenu.Popup>
                    </ContextMenu.Positioner>
                  </ContextMenu.Portal>
                </ContextMenu.SubmenuRoot>
              </>
            ) : null}
          </ContextMenu.Popup>
        </ContextMenu.Positioner>
      </ContextMenu.Portal>
    </ContextMenu.Root>
  );
}

const meta = {
  title: "Components/ContextMenu",
  component: ProjectCardMenu,
  args: {
    onOpenChange: fn(),
  },
} satisfies Meta<typeof ProjectCardMenu>;

export default meta;

type Story = StoryObj<typeof meta>;

/**
 * The closed card — the state a page shows almost all of the time, and the reason
 * the trigger recipe paints so little: everything visible here is the caller's.
 */
export const Default: Story = {};

/** The open menu: section label, three rows, and the card ringed behind it. */
export const Open: Story = {
  args: { defaultOpen: true },
};

/** The state-carrying rows: a checkbox row and a radio group, separated from the commands. */
export const WithSelectionItems: Story = {
  args: { defaultOpen: true, withSelection: true },
};

/** A nested menu, opened from its own row. */
export const WithSubmenu: Story = {
  args: { defaultOpen: true, withSubmenu: true },
  play: async () => {
    await userEvent.click(await screen.findByTestId("card-move"));

    const submenu = await screen.findByTestId("card-move-popup");
    await waitFor(async () => {
      await expect(submenu).toBeVisible();
    });
    await expect(submenu).toHaveAttribute("data-nested");
    await expect(screen.getByTestId("card-move")).toHaveAttribute("data-popup-open");
    // The parent menu stays on screen behind its child.
    await expect(screen.getByTestId("card-popup")).toHaveAttribute("data-open");
  },
};

/**
 * The interaction this component exists for: a right click on the card opens the
 * menu at the pointer, and choosing a row closes it. There is deliberately no
 * left-click and no keyboard path — a context-menu trigger is a plain `<div>` and
 * opens on `contextmenu` alone.
 */
export const RightClickToOpen: Story = {
  play: async ({ args, canvas }) => {
    const trigger = canvas.getByTestId("card-trigger");

    await userEvent.pointer({ target: trigger, keys: "[MouseRight]" });

    // The popup is portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("card-popup");
    // The popup recipe carries a 150ms enter transition starting at opacity-0, so
    // it is not "visible" until that settles.
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(popup).toHaveAttribute("data-open");
    await expect(trigger).toHaveAttribute("data-popup-open");
    await expect(args.onOpenChange).toHaveBeenCalledWith(true, expect.anything());

    await userEvent.click(screen.getByTestId("card-rename"));

    await waitFor(async () => {
      await expect(screen.queryByTestId("card-popup")).not.toBeInTheDocument();
    });
    await expect(trigger).not.toHaveAttribute("data-popup-open");
  },
};

/**
 * The controlled shape: the open state lives in the caller's `useState`, and the
 * menu reports every change back through `onOpenChange`. This is what a consumer
 * copies when the same menu also has to open from a toolbar button — the trigger is
 * still the right-click surface, but it no longer owns the state.
 */
export const Controlled: Story = {
  render: function ControlledContextMenu(args) {
    const [open, setOpen] = useState(false);

    return (
      <div className="flex flex-col gap-2">
        <button
          type="button"
          data-testid="controlled-external"
          className="text-sm text-foreground"
          onClick={() => setOpen(true)}
        >
          Open from outside the menu
        </button>
        <span data-testid="controlled-state">{open ? "open" : "closed"}</span>
        <ContextMenu.Root
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            args.onOpenChange?.(next);
          }}
        >
          <ContextMenu.Trigger
            data-testid="controlled-trigger"
            className="h-24 w-56 border border-border bg-card p-4 text-sm text-card-foreground"
          >
            …or right click here
          </ContextMenu.Trigger>
          <ContextMenu.Portal>
            <ContextMenu.Positioner>
              <ContextMenu.Popup data-testid="controlled-popup">
                <ContextMenu.Item data-testid="controlled-duplicate">Duplicate</ContextMenu.Item>
                <ContextMenu.Item data-testid="controlled-rename">Rename</ContextMenu.Item>
              </ContextMenu.Popup>
            </ContextMenu.Positioner>
          </ContextMenu.Portal>
        </ContextMenu.Root>
      </div>
    );
  },
  play: async ({ canvas }) => {
    await userEvent.click(canvas.getByTestId("controlled-external"));

    const popup = await screen.findByTestId("controlled-popup");
    await expect(popup).toHaveAttribute("data-open");
    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("open");

    await userEvent.click(screen.getByTestId("controlled-duplicate"));

    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("closed");
    await waitFor(async () => {
      await expect(screen.queryByTestId("controlled-popup")).not.toBeInTheDocument();
    });
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to a
 * `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented class
 * names passes context-menu.test.tsx's class-set assertions and still paints
 * nothing. These assertions read computed styles instead of class names.
 *
 * The trigger's are this component's own; everything below it is inherited from the
 * Menu component's recipes and pinned here so a swap away from those shared
 * wrappers cannot go unnoticed.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { defaultOpen: true, withSelection: true },
  play: async () => {
    // `select-none` on the trigger, so a right-drag opens the menu instead of
    // selecting the card's text.
    const trigger = await screen.findByTestId("card-trigger");
    const triggerStyle = getComputedStyle(trigger);
    await expect(triggerStyle.userSelect).toBe("none");
    // `rounded-md`, and the `data-[popup-open]:ring-2 ring-ring` this story's open
    // menu switches on. Tailwind's ring is a box-shadow, so an unringed element
    // reads "none" — never read `.transform` or `.outline` for this.
    await expect(triggerStyle.borderTopLeftRadius).not.toBe("0px");
    await expect(triggerStyle.boxShadow).not.toBe("none");

    // Inherited from the Menu recipes below here. `z-50` on the positioner, on top
    // of the inline positioning Base UI owns.
    const positioner = await screen.findByTestId("card-positioner");
    await expect(getComputedStyle(positioner).zIndex).toBe("50");

    // `min-w-32`, `rounded-md`, `border border-border`, `bg-popover` and `p-1` on
    // the popup, against the unstyled defaults of `auto`, `0px` and transparent.
    const popupStyle = getComputedStyle(await screen.findByTestId("card-popup"));
    await expect(popupStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(popupStyle.borderTopWidth).not.toBe("0px");
    await expect(popupStyle.paddingTop).not.toBe("0px");
    await expect(popupStyle.borderTopLeftRadius).not.toBe("0px");
    await expect(popupStyle.minWidth).not.toBe("0px");

    // `fixed inset-0` on the backdrop: it has to cover the window, or an outside
    // press lands on the page instead of closing the menu.
    const backdropStyle = getComputedStyle(await screen.findByTestId("card-backdrop"));
    await expect(backdropStyle.position).toBe("fixed");

    // `px-2 py-1.5 text-sm` on a row, and `text-xs text-muted-foreground` on the
    // section label above it — the label has to read smaller than its rows.
    const rowStyle = getComputedStyle(await screen.findByTestId("card-duplicate"));
    await expect(rowStyle.paddingLeft).not.toBe("0px");
    await expect(rowStyle.paddingTop).not.toBe("0px");

    const labelStyle = getComputedStyle(await screen.findByTestId("card-group-label"));
    await expect(Number.parseFloat(labelStyle.fontSize)).toBeLessThan(
      Number.parseFloat(rowStyle.fontSize),
    );

    // `h-px bg-border` on the separator: a hairline rule with a real colour.
    const separatorStyle = getComputedStyle(await screen.findByTestId("card-separator"));
    await expect(separatorStyle.height).toBe("1px");
    await expect(separatorStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");

    // `relative pl-8` on the checkbox row: its gutter has to be wider than a plain
    // row's padding, or a mounted indicator would sit on the label.
    const checkboxStyle = getComputedStyle(await screen.findByTestId("card-grid"));
    await expect(Number.parseFloat(checkboxStyle.paddingLeft)).toBeGreaterThan(
      Number.parseFloat(rowStyle.paddingLeft),
    );

    // `absolute left-2` on the checked radio row's indicator, which is the only
    // indicator in the DOM at all.
    const indicatorStyle = getComputedStyle(
      await screen.findByTestId("card-density-cosy-indicator"),
    );
    await expect(indicatorStyle.position).toBe("absolute");
  },
};
