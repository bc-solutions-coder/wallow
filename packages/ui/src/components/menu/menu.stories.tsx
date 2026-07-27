import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { Menu } from "./menu";

/*
 * Wallow-m5aq.3.6 — Menu stories. `@storybook/addon-vitest` turns every export
 * below into a Vitest test case rendered in the same headless Chromium the
 * `browser` project uses, with the real Tailwind pipeline attached (see
 * .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while menu.test.tsx holds the markup assertions a screenshot cannot make.
 *
 * Two things belong HERE rather than in menu.test.tsx:
 *
 *   - POINTER interaction inside the open popup. Base UI always renders a fixed,
 *     full-window pointer blocker inside the portal, and `vitest/browser`'s
 *     `userEvent` drives real Playwright input, which hit-tests the click point
 *     and therefore hits the blocker instead of the row. `userEvent` here is
 *     `@testing-library/user-event` (bundled by `storybook/test`), which
 *     dispatches synthetic events straight at the element with no hit-testing,
 *     so clicking a row inside an open menu just works.
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens) — this project compiles real Tailwind and the
 *     `browser` project does not.
 *
 * A NOTE ON `toBeVisible()`: the popup recipe carries a 150ms enter transition
 * that starts at `opacity-0`, so the popup is not "visible" for the duration of
 * that transition. Every visibility assertion below is wrapped in `waitFor` —
 * asserting it synchronously right after opening is the failure the Dialog
 * exemplar hit and pinned for the whole wave.
 *
 * `Menu.Viewport` is deliberately absent from these stories: it only earns its
 * keep when ONE popup is opened by several triggers and the content cross-fades
 * between them, which no realistic single-trigger story shows. Its markup is
 * pinned in menu.test.tsx instead.
 */

interface ProjectMenuProps {
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
 * A complete, realistic menu — the story subject. Stories drive the real `Menu`
 * namespace through this so every part is exercised together rather than one
 * part at a time.
 */
function ProjectMenu({
  defaultOpen,
  withSelection,
  withSubmenu,
  onOpenChange,
}: ProjectMenuProps): ReactElement {
  return (
    <Menu.Root defaultOpen={defaultOpen} onOpenChange={onOpenChange}>
      <Menu.Trigger data-testid="project-trigger">Project actions</Menu.Trigger>
      <Menu.Portal>
        <Menu.Backdrop data-testid="project-backdrop" />
        <Menu.Positioner data-testid="project-positioner" sideOffset={8}>
          <Menu.Popup data-testid="project-popup">
            <Menu.Arrow data-testid="project-arrow" />
            <Menu.Group data-testid="project-group">
              <Menu.GroupLabel data-testid="project-group-label">Project</Menu.GroupLabel>
              <Menu.Item data-testid="project-duplicate">Duplicate</Menu.Item>
              <Menu.Item data-testid="project-rename">Rename</Menu.Item>
              <Menu.LinkItem data-testid="project-docs" href="https://example.com/docs">
                Open documentation
              </Menu.LinkItem>
            </Menu.Group>
            {withSelection ? (
              <>
                <Menu.Separator data-testid="project-separator" />
                <Menu.CheckboxItem data-testid="project-grid">
                  <Menu.CheckboxItemIndicator data-testid="project-grid-indicator">
                    ✓
                  </Menu.CheckboxItemIndicator>
                  Show grid
                </Menu.CheckboxItem>
                <Menu.RadioGroup data-testid="project-density" defaultValue="cosy">
                  <Menu.RadioItem data-testid="project-density-cosy" value="cosy">
                    <Menu.RadioItemIndicator data-testid="project-density-cosy-indicator">
                      •
                    </Menu.RadioItemIndicator>
                    Cosy
                  </Menu.RadioItem>
                  <Menu.RadioItem data-testid="project-density-compact" value="compact">
                    <Menu.RadioItemIndicator data-testid="project-density-compact-indicator">
                      •
                    </Menu.RadioItemIndicator>
                    Compact
                  </Menu.RadioItem>
                </Menu.RadioGroup>
              </>
            ) : null}
            {withSubmenu ? (
              <>
                <Menu.Separator />
                <Menu.SubmenuRoot>
                  <Menu.SubmenuTrigger data-testid="project-move">Move to</Menu.SubmenuTrigger>
                  <Menu.Portal>
                    <Menu.Positioner data-testid="project-move-positioner">
                      <Menu.Popup data-testid="project-move-popup">
                        <Menu.Item data-testid="project-move-archive">Archive</Menu.Item>
                        <Menu.Item data-testid="project-move-trash">Trash</Menu.Item>
                      </Menu.Popup>
                    </Menu.Positioner>
                  </Menu.Portal>
                </Menu.SubmenuRoot>
              </>
            ) : null}
          </Menu.Popup>
        </Menu.Positioner>
      </Menu.Portal>
    </Menu.Root>
  );
}

const meta = {
  title: "Components/Menu",
  component: ProjectMenu,
  args: {
    onOpenChange: fn(),
  },
} satisfies Meta<typeof ProjectMenu>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The closed trigger — the state a page shows most of the time. */
export const Default: Story = {};

/** The open menu: anchored popup, section label, three rows and the arrow. */
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
    await userEvent.click(await screen.findByTestId("project-move"));

    const submenu = await screen.findByTestId("project-move-popup");
    await waitFor(async () => {
      await expect(submenu).toBeVisible();
    });
    await expect(submenu).toHaveAttribute("data-nested");
    await expect(screen.getByTestId("project-move")).toHaveAttribute("data-popup-open");
    // The parent menu stays on screen behind its child.
    await expect(screen.getByTestId("project-popup")).toHaveAttribute("data-open");
  },
};

/**
 * The controlled shape: the open state lives in the caller's `useState`, and the
 * menu reports every change back through `onOpenChange`. This is the story a
 * consumer copies when the menu has to open from somewhere other than its own
 * trigger.
 */
export const Controlled: Story = {
  render: function ControlledMenu(args) {
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
        <Menu.Root
          open={open}
          onOpenChange={(next) => {
            setOpen(next);
            args.onOpenChange?.(next);
          }}
        >
          <Menu.Portal>
            <Menu.Positioner>
              <Menu.Popup data-testid="controlled-popup">
                <Menu.Item data-testid="controlled-duplicate">Duplicate</Menu.Item>
                <Menu.Item data-testid="controlled-rename">Rename</Menu.Item>
              </Menu.Popup>
            </Menu.Positioner>
          </Menu.Portal>
        </Menu.Root>
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

/** The interaction half: opening from the trigger, then choosing a row. */
export const OpenAndDismiss: Story = {
  play: async ({ args, canvas }) => {
    const trigger = canvas.getByTestId("project-trigger");

    await userEvent.click(trigger);

    // The popup is portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("project-popup");
    // The popup recipe carries a 150ms enter transition starting at opacity-0,
    // so it is not "visible" until that settles.
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(popup).toHaveAttribute("data-open");
    await expect(trigger).toHaveAttribute("data-popup-open");
    await expect(args.onOpenChange).toHaveBeenCalledWith(true, expect.anything());

    // Real Tailwind is loaded here, so the popup's `z-50` puts it above Base
    // UI's own pointer blocker and a genuine click lands. The `browser` project
    // cannot do this — see the header.
    await userEvent.click(screen.getByTestId("project-rename"));

    await waitFor(async () => {
      await expect(screen.queryByTestId("project-popup")).not.toBeInTheDocument();
    });
    await expect(trigger).not.toHaveAttribute("data-popup-open");
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes menu.test.tsx's class-set assertions and still paints
 * nothing. These assertions read computed styles instead of class names.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { defaultOpen: true, withSelection: true },
  play: async () => {
    // `z-50` on the positioner, on top of the inline positioning Base UI owns.
    const positioner = await screen.findByTestId("project-positioner");
    await expect(getComputedStyle(positioner).zIndex).toBe("50");

    // `min-w-32`, `rounded-md`, `border border-border`, `bg-popover` and `p-1`
    // on the popup, against the unstyled defaults of `auto`, `0px` and
    // transparent.
    const popupStyle = getComputedStyle(await screen.findByTestId("project-popup"));
    await expect(popupStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(popupStyle.borderTopWidth).not.toBe("0px");
    await expect(popupStyle.borderTopStyle).toBe("solid");
    await expect(popupStyle.paddingTop).not.toBe("0px");
    await expect(popupStyle.borderTopLeftRadius).not.toBe("0px");
    await expect(popupStyle.minWidth).not.toBe("0px");

    // `px-2 py-1.5 text-sm` on a row, and `text-xs text-muted-foreground` on the
    // section label above it — the label has to read smaller than its rows.
    const rowStyle = getComputedStyle(await screen.findByTestId("project-duplicate"));
    await expect(rowStyle.paddingLeft).not.toBe("0px");
    await expect(rowStyle.paddingTop).not.toBe("0px");

    const labelStyle = getComputedStyle(await screen.findByTestId("project-group-label"));
    await expect(Number.parseFloat(labelStyle.fontSize)).toBeLessThan(
      Number.parseFloat(rowStyle.fontSize),
    );

    // `h-px bg-border` on the separator: a hairline rule with a real colour.
    const separatorStyle = getComputedStyle(await screen.findByTestId("project-separator"));
    await expect(separatorStyle.height).toBe("1px");
    await expect(separatorStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");

    // `relative pl-8` on the checkbox row: its gutter has to be wider than a
    // plain row's padding, or a mounted indicator would sit on the label.
    const checkboxStyle = getComputedStyle(await screen.findByTestId("project-grid"));
    await expect(Number.parseFloat(checkboxStyle.paddingLeft)).toBeGreaterThan(
      Number.parseFloat(rowStyle.paddingLeft),
    );

    // `absolute left-2` on the checked radio row's indicator, which is the only
    // indicator in the DOM at all.
    const indicatorStyle = getComputedStyle(
      await screen.findByTestId("project-density-cosy-indicator"),
    );
    await expect(indicatorStyle.position).toBe("absolute");

    // `size-2.5 border border-border bg-popover` on the arrow.
    const arrowStyle = getComputedStyle(await screen.findByTestId("project-arrow"));
    await expect(arrowStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(arrowStyle.borderTopWidth).not.toBe("0px");
  },
};
