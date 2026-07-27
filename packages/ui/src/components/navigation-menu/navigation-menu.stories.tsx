import type { Meta, StoryObj } from "@storybook/react-vite";
import { type ReactElement, useState } from "react";
import { expect, fn, screen, userEvent, waitFor } from "storybook/test";

import { NavigationMenu } from "./navigation-menu";

/*
 * Wallow-m5aq.3.9 — Navigation Menu stories. `@storybook/addon-vitest` turns
 * every export below into a Vitest test case rendered in the same headless
 * Chromium the `browser` project uses, with the real Tailwind pipeline attached
 * (see .storybook/main.ts), so these are the VISUAL half of the component's spec
 * while navigation-menu.test.tsx holds the markup assertions a screenshot cannot
 * make.
 *
 * Three things belong HERE rather than in navigation-menu.test.tsx:
 *
 *   - POINTER interaction while the panel is open. `vitest/browser`'s
 *     `userEvent` drives real Playwright input, which hit-tests the click point;
 *     the `browser` project compiles no Tailwind, so the popup's `z-50` is inert
 *     and the open panel sits on top of the trigger list, and a click on a
 *     second trigger there never resolves. `userEvent` here is
 *     `@testing-library/user-event` (bundled by `storybook/test`), which
 *     dispatches synthetic events straight at the element with no hit-testing.
 *   - Any assertion that a recipe utility actually PAINTS (see
 *     PaintedByTheDesignTokens) — this project compiles real Tailwind and the
 *     `browser` project does not.
 *   - The EXPANDED vs COLLAPSED presentation, which is the bead's own acceptance
 *     criterion and is a composition of caller `className` and caller markup
 *     rather than anything the recipes decide. ExpandedSidebar and
 *     CollapsedIconRail render the SAME tree with one prop flipped.
 *
 * TWO STANDING RULES THIS FILE OBEYS:
 *
 *   - `toBeVisible()` is always wrapped in `waitFor`. The popup recipe carries a
 *     150ms enter transition starting at `opacity-0`, so the popup is not
 *     "visible" for the duration of that transition; asserting it synchronously
 *     right after opening is the failure the Dialog exemplar pinned for the
 *     whole wave.
 *   - EVERY `href` BELOW IS A `#hash`. `NavigationMenu.Link` renders a genuine
 *     `<a>`, and a real click on an absolute URL navigates the test iframe,
 *     which kills the whole run with "Cannot connect to the iframe" — not one
 *     failing story, all of them. Dismissal below therefore goes through Escape
 *     rather than through pressing a link.
 */

/** Which of the two sidebar presentations the subject renders. */
type NavMode = "expanded" | "rail";

interface SiteNavProps {
  /** `rail` collapses every row to its glyph and moves the label into `aria-label`. */
  readonly mode?: NavMode;
  /** Opens one section on first render, for the screenshot stories. */
  readonly defaultValue?: string;
  /** Called with the section value the menu wants open. */
  readonly onValueChange?: (value: string | null) => void;
}

interface NavRow {
  readonly value: string;
  readonly label: string;
  readonly glyph: string;
  readonly links: readonly { readonly href: string; readonly label: string }[];
}

const SECTIONS: readonly NavRow[] = [
  {
    value: "storage",
    label: "Storage",
    glyph: "▤",
    links: [
      { href: "#files", label: "Files" },
      { href: "#buckets", label: "Buckets" },
    ],
  },
  {
    value: "settings",
    label: "Settings",
    glyph: "⚙",
    links: [
      { href: "#branding", label: "Branding" },
      { href: "#api-keys", label: "API keys" },
    ],
  },
];

/**
 * A realistic dashboard sidebar — the story subject. Stories drive the real
 * `NavigationMenu` namespace through this so every part is exercised together
 * rather than one part at a time, and so the expanded and collapsed
 * presentations are provably the same component.
 *
 * The rail is a caller composition, not a recipe variant: a narrower `Root`, a
 * label swapped for an `aria-label`, and the chevron dropped. The recipes are
 * layout-neutral (`min-w-0`, no fixed width) exactly so this works.
 */
function SiteNav({ mode = "expanded", defaultValue, onValueChange }: SiteNavProps): ReactElement {
  const rail = mode === "rail";

  return (
    <NavigationMenu.Root
      data-testid="sidebar"
      className={rail ? "w-16 flex-col" : "w-64 flex-col"}
      orientation="vertical"
      defaultValue={defaultValue}
      onValueChange={onValueChange}
    >
      <NavigationMenu.List data-testid="sidebar-list" className="flex-col">
        <NavigationMenu.Item>
          <NavigationMenu.Link
            data-testid="sidebar-overview"
            href="#overview"
            active
            aria-label={rail ? "Overview" : undefined}
            className={rail ? "justify-center" : undefined}
          >
            <span aria-hidden>◧</span>
            {rail ? null : "Overview"}
          </NavigationMenu.Link>
        </NavigationMenu.Item>
        {SECTIONS.map((section) => (
          <NavigationMenu.Item key={section.value} value={section.value}>
            <NavigationMenu.Trigger
              data-testid={`sidebar-${section.value}`}
              aria-label={rail ? section.label : undefined}
              className={rail ? "w-full justify-center" : undefined}
            >
              <span aria-hidden>{section.glyph}</span>
              {rail ? null : section.label}
              {rail ? null : (
                <NavigationMenu.Icon data-testid={`sidebar-${section.value}-icon`}>
                  ⌄
                </NavigationMenu.Icon>
              )}
            </NavigationMenu.Trigger>
            <NavigationMenu.Content data-testid={`sidebar-${section.value}-panel`}>
              {section.links.map((link) => (
                <NavigationMenu.Link
                  key={link.href}
                  data-testid={`sidebar-link-${link.href.slice(1)}`}
                  href={link.href}
                >
                  {link.label}
                </NavigationMenu.Link>
              ))}
            </NavigationMenu.Content>
          </NavigationMenu.Item>
        ))}
      </NavigationMenu.List>
      <NavigationMenu.Portal>
        <NavigationMenu.Positioner
          data-testid="sidebar-positioner"
          side="inline-end"
          align="start"
          sideOffset={8}
        >
          <NavigationMenu.Popup data-testid="sidebar-popup">
            <NavigationMenu.Arrow data-testid="sidebar-arrow" />
            <NavigationMenu.Viewport data-testid="sidebar-viewport" />
          </NavigationMenu.Popup>
        </NavigationMenu.Positioner>
      </NavigationMenu.Portal>
    </NavigationMenu.Root>
  );
}

const meta = {
  title: "Components/NavigationMenu",
  component: SiteNav,
  args: {
    onValueChange: fn(),
  },
} satisfies Meta<typeof SiteNav>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The closed sidebar — the state a dashboard shows most of the time. */
export const Default: Story = {};

/** A section open: the anchored panel beside the rail, its arrow and its links. */
export const Open: Story = {
  args: { defaultValue: "storage" },
};

/**
 * The EXPANDED sidebar (acceptance criterion): full-width rows carrying a glyph,
 * a label and, on the rows that open a panel, the chevron.
 */
export const ExpandedSidebar: Story = {
  args: { mode: "expanded", defaultValue: "storage" },
};

/**
 * The COLLAPSED icon rail (acceptance criterion): the SAME tree at `w-16` with
 * each label moved into `aria-label` and the chevron dropped. This is the story
 * that proves the recipes stay out of the caller's way — no recipe utility fixes
 * a width, and `min-w-0` on Root, List and every row is what stops the rail
 * being blown open.
 */
export const CollapsedIconRail: Story = {
  args: { mode: "rail", defaultValue: "storage" },
  play: async ({ canvas }) => {
    const rail = canvas.getByTestId("sidebar");
    // 64px, not one pixel wider — the whole point of the rail.
    await expect(rail.getBoundingClientRect().width).toBe(64);

    // The label survives for assistive tech even though no text is rendered.
    await expect(canvas.getByTestId("sidebar-storage")).toHaveAttribute("aria-label", "Storage");
    await expect(canvas.getByTestId("sidebar-storage")).toHaveTextContent("▤");
    await expect(canvas.queryByTestId("sidebar-storage-icon")).not.toBeInTheDocument();

    // The panel still opens beside the rail at full width.
    const popup = await screen.findByTestId("sidebar-popup");
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(popup.getBoundingClientRect().left).toBeGreaterThan(
      rail.getBoundingClientRect().right,
    );
  },
};

/**
 * The mobile presentation: the backdrop, which is a bare outside-press catcher
 * by default, is dressed as a real scrim through `className`. This is the third
 * mode the dashboard sidebar needs, and the reason `navigationMenuBackdropRecipe`
 * ships no background of its own — a desktop dropdown must not dim the page.
 */
export const MobileOverlay: Story = {
  render: function MobileNav() {
    return (
      <div className="relative h-64 w-80 overflow-hidden">
        <p className="p-4 text-sm text-foreground">Page content behind the drawer.</p>
        <NavigationMenu.Root
          data-testid="drawer"
          className="absolute inset-y-0 left-0 w-64 flex-col bg-background p-2"
          orientation="vertical"
          defaultValue="storage"
        >
          <NavigationMenu.List data-testid="drawer-list" className="flex-col">
            <NavigationMenu.Item value="storage">
              <NavigationMenu.Trigger data-testid="drawer-storage">
                Storage
                <NavigationMenu.Icon>⌄</NavigationMenu.Icon>
              </NavigationMenu.Trigger>
              <NavigationMenu.Content data-testid="drawer-panel">
                <NavigationMenu.Link data-testid="drawer-files" href="#files">
                  Files
                </NavigationMenu.Link>
              </NavigationMenu.Content>
            </NavigationMenu.Item>
          </NavigationMenu.List>
          <NavigationMenu.Portal>
            <NavigationMenu.Backdrop data-testid="drawer-backdrop" className="bg-foreground/50" />
            <NavigationMenu.Positioner data-testid="drawer-positioner" side="inline-end">
              <NavigationMenu.Popup data-testid="drawer-popup">
                <NavigationMenu.Viewport />
              </NavigationMenu.Popup>
            </NavigationMenu.Positioner>
          </NavigationMenu.Portal>
        </NavigationMenu.Root>
      </div>
    );
  },
  play: async () => {
    const backdrop = await screen.findByTestId("drawer-backdrop");
    // The caller's scrim wins over the recipe's bare `fixed inset-0`, and the
    // recipe's own covering survives underneath it.
    await expect(getComputedStyle(backdrop).backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(getComputedStyle(backdrop).position).toBe("fixed");
  },
};

/**
 * The controlled shape: the open section lives in the caller's `useState` and
 * the menu reports every change back through `onValueChange`. This is the story
 * a consumer copies when the sidebar's open section is derived from the route.
 *
 * The two buttons are deliberately IDEMPOTENT rather than one toggle: a toggle
 * button beside a menu that closes on outside press flips the state twice per
 * click (the press closes the menu, then the handler reopens it), which is the
 * trap Wallow-m5aq.3.5 pinned.
 */
export const Controlled: Story = {
  render: function ControlledNav(args) {
    const [value, setValue] = useState<string | null>(null);

    return (
      <div className="flex flex-col gap-2">
        <div className="flex gap-2">
          <button
            type="button"
            data-testid="controlled-open"
            className="text-sm text-foreground"
            onClick={() => setValue("storage")}
          >
            Open storage
          </button>
          <button
            type="button"
            data-testid="controlled-close"
            className="text-sm text-foreground"
            onClick={() => setValue(null)}
          >
            Close
          </button>
        </div>
        <span data-testid="controlled-state">{value ?? "closed"}</span>
        <NavigationMenu.Root
          data-testid="controlled-root"
          className="flex-col"
          orientation="vertical"
          value={value}
          onValueChange={(next) => {
            setValue(next);
            args.onValueChange?.(next);
          }}
        >
          <NavigationMenu.List className="flex-col">
            <NavigationMenu.Item value="storage">
              <NavigationMenu.Trigger data-testid="controlled-storage">
                Storage
                <NavigationMenu.Icon>⌄</NavigationMenu.Icon>
              </NavigationMenu.Trigger>
              <NavigationMenu.Content data-testid="controlled-panel">
                <NavigationMenu.Link data-testid="controlled-files" href="#files">
                  Files
                </NavigationMenu.Link>
              </NavigationMenu.Content>
            </NavigationMenu.Item>
          </NavigationMenu.List>
          <NavigationMenu.Portal>
            <NavigationMenu.Positioner>
              <NavigationMenu.Popup data-testid="controlled-popup">
                <NavigationMenu.Viewport />
              </NavigationMenu.Popup>
            </NavigationMenu.Positioner>
          </NavigationMenu.Portal>
        </NavigationMenu.Root>
      </div>
    );
  },
  play: async ({ canvas }) => {
    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("closed");

    await userEvent.click(canvas.getByTestId("controlled-open"));

    // The popup is portalled to <body>, so it is not inside `canvas`.
    const popup = await screen.findByTestId("controlled-popup");
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(popup).toHaveAttribute("data-open");
    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("storage");
    await expect(canvas.getByTestId("controlled-storage")).toHaveAttribute("data-popup-open");

    await userEvent.click(canvas.getByTestId("controlled-close"));

    await waitFor(async () => {
      await expect(screen.queryByTestId("controlled-popup")).not.toBeInTheDocument();
    });
    await expect(canvas.getByTestId("controlled-state")).toHaveTextContent("closed");
  },
};

/**
 * The interaction half: opening a section from its trigger, switching to the
 * next one inside the SAME popup, then dismissing.
 *
 * Switching sections is the interaction navigation-menu.test.tsx cannot drive
 * with a real pointer (the open panel intercepts it there — see the header), so
 * this story is where the pointer actually does it. Dismissal is Escape, not a
 * link press, for the iframe-navigation reason in the header.
 *
 * ATTENTION — SWITCHING SECTIONS IS A `hover`, NEVER A SECOND `click`. Measured:
 * `userEvent.click` on a second trigger while a panel is open CLOSES the whole
 * menu here (both triggers end at `aria-expanded="false"` and the portal
 * unmounts). `storybook/test`'s `userEvent` is
 * `@testing-library/user-event`, whose synthetic press begins with a
 * `pointerdown` outside the portalled popup, and Base UI reads that as an
 * outside press before the trigger's own press handler runs. `userEvent.hover`
 * switches cleanly — and is what a real user does to a navigation menu anyway,
 * since the component opens on hover. (The `browser` project has the mirror-image
 * constraint: no hover trouble, but a real click cannot reach the second trigger
 * under the open panel, so it uses a direct `element.click()` instead.)
 */
export const OpenAndDismiss: Story = {
  play: async ({ args, canvas }) => {
    const storage = canvas.getByTestId("sidebar-storage");

    await userEvent.click(storage);

    const popup = await screen.findByTestId("sidebar-popup");
    // The popup recipe carries a 150ms enter transition starting at opacity-0,
    // so it is not "visible" until that settles.
    await waitFor(async () => {
      await expect(popup).toBeVisible();
    });
    await expect(popup).toHaveAttribute("data-open");
    await expect(storage).toHaveAttribute("data-popup-open");
    await expect(canvas.getByTestId("sidebar-storage-icon")).toHaveAttribute("data-popup-open");
    await expect(args.onValueChange).toHaveBeenCalledWith("storage", expect.anything());

    // The second section reuses the same popup; only what is in the viewport
    // changes. Switching goes through HOVER, not a second click — see the
    // ATTENTION note under this story.
    await userEvent.hover(canvas.getByTestId("sidebar-settings"));

    await waitFor(async () => {
      await expect(screen.queryByTestId("sidebar-storage-panel")).not.toBeInTheDocument();
    });
    const settingsPanel = await screen.findByTestId("sidebar-settings-panel");
    await expect(screen.getByTestId("sidebar-viewport")).toContainElement(settingsPanel);
    await expect(screen.getByTestId("sidebar-popup")).toBe(popup);
    await expect(storage).not.toHaveAttribute("data-popup-open");
    await expect(canvas.getByTestId("sidebar-settings")).toHaveAttribute("data-popup-open");

    await userEvent.keyboard("{Escape}");

    await waitFor(async () => {
      await expect(screen.queryByTestId("sidebar-popup")).not.toBeInTheDocument();
    });
    await expect(canvas.getByTestId("sidebar-settings")).not.toHaveAttribute("data-popup-open");
  },
};

/**
 * The recipes' utilities have to be REAL — emitted by Tailwind and resolving to
 * a `@bc-solutions-coder/styles` token. Only this project can prove that: the
 * `browser` vitest project compiles no Tailwind, so a recipe full of invented
 * class names passes navigation-menu.test.tsx's class-set assertions and still
 * paints nothing. These assertions read computed styles instead of class names.
 */
export const PaintedByTheDesignTokens: Story = {
  args: { defaultValue: "storage" },
  play: async ({ canvas }) => {
    // `min-w-0` on the root: the utility the icon rail depends on, against a
    // `<nav>`'s default `auto`.
    await expect(getComputedStyle(canvas.getByTestId("sidebar")).minWidth).toBe("0px");

    // `m-0 list-none p-0` on the list, against a `<ul>`'s 40px inline-start
    // padding and its bullets.
    const listStyle = getComputedStyle(canvas.getByTestId("sidebar-list"));
    await expect(listStyle.paddingInlineStart).toBe("0px");
    await expect(listStyle.marginBlockStart).toBe("0px");
    await expect(listStyle.listStyleType).toBe("none");

    // `flex gap-3 px-3 py-2 text-sm` on a row.
    const triggerStyle = getComputedStyle(canvas.getByTestId("sidebar-storage"));
    await expect(triggerStyle.display).toBe("flex");
    await expect(triggerStyle.paddingLeft).not.toBe("0px");
    await expect(triggerStyle.paddingTop).not.toBe("0px");
    await expect(triggerStyle.columnGap).not.toBe("normal");

    // `no-underline` on a link, against a browser's default underline, and
    // `data-[active]:bg-accent` on the one that names itself the current page.
    const overviewStyle = getComputedStyle(canvas.getByTestId("sidebar-overview"));
    await expect(overviewStyle.textDecorationLine).toBe("none");
    await expect(overviewStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");

    // `size-4` on the chevron.
    const iconStyle = getComputedStyle(canvas.getByTestId("sidebar-storage-icon"));
    await expect(iconStyle.width).toBe("16px");

    // `z-50` on the positioner, on top of the inline positioning Base UI owns.
    const positioner = await screen.findByTestId("sidebar-positioner");
    await expect(getComputedStyle(positioner).zIndex).toBe("50");

    // `relative rounded-md border border-border bg-popover shadow-md` on the
    // popup. `relative` is load-bearing: the arrow is positioned against it.
    const popupStyle = getComputedStyle(await screen.findByTestId("sidebar-popup"));
    await expect(popupStyle.position).toBe("relative");
    await expect(popupStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(popupStyle.borderTopWidth).not.toBe("0px");
    await expect(popupStyle.borderTopStyle).toBe("solid");
    await expect(popupStyle.borderTopLeftRadius).not.toBe("0px");
    await expect(popupStyle.boxShadow).not.toBe("none");

    // `flex flex-col p-2` on the panel — the padding lives here, not on the
    // shared popup.
    const panelStyle = getComputedStyle(await screen.findByTestId("sidebar-storage-panel"));
    await expect(panelStyle.flexDirection).toBe("column");
    await expect(panelStyle.paddingTop).not.toBe("0px");

    // `overflow-hidden` on the viewport, which is what lets panels cross-fade.
    await expect(getComputedStyle(await screen.findByTestId("sidebar-viewport")).overflow).toBe(
      "hidden",
    );

    // `size-2.5 border border-border bg-popover` on the arrow, plus the
    // per-side offset the recipe supplies because Base UI sets no cross-axis
    // inset of its own.
    const arrowStyle = getComputedStyle(await screen.findByTestId("sidebar-arrow"));
    await expect(arrowStyle.position).toBe("absolute");
    await expect(arrowStyle.backgroundColor).not.toBe("rgba(0, 0, 0, 0)");
    await expect(arrowStyle.borderTopWidth).not.toBe("0px");
  },
};
