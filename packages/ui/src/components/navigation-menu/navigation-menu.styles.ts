import { cva, type VariantProps } from "class-variance-authority";

/*
 * The class lists are not invented here — navigation-menu.test.tsx declares each
 * part's exact utility set as a top-of-file `*_CLASSES` constant and asserts it
 * as an order-free set through the rendered component, so that spec is the
 * source of truth for everything below.
 *
 * One recipe per styled part of the navigation menu. Twelve of Base UI's
 * thirteen namespace members get one — only `Portal` does not, because it
 * renders just the structural container Base UI appends to `<body>` (see
 * navigation-menu.tsx).
 *
 * No STATE is a cva variant, for the reason the whole Base UI catalog settled
 * on: open/closed, active, disabled and the entering/exiting transition phases
 * are all states, and Base UI publishes states as `data-*` attributes, so they
 * belong in the base string as `data-[popup-open]:` / `data-[active]:` /
 * `data-[starting-style]:` / `data-[ending-style]:` modifiers rather than as cva
 * variants nobody would pass by hand. The disabled state is the one exception to
 * the `data-*` habit: measured, a disabled `NavigationMenu.Trigger` renders
 * `aria-disabled="true"` and stays focusable — it gets no `data-disabled` at all
 * — so its recipe keys off `aria-disabled:`.
 *
 * `surface` on the trigger and link recipes is the one cva variant here, and it
 * is not a state: nothing in the DOM says which surface a caller composed the
 * row onto, so it cannot be a `data-*` modifier and has to be passed. Both nav
 * rows carry the same axis with the same two arms, because a trigger and a link
 * sit in the same list and have to paint alike. See the recipes.
 *
 * The expanded-vs-icon-rail axis the Phase-4 sidebar needs is NOT a variant
 * either, and that is a deliberate call rather than an omission. A rail is a
 * width plus a hidden label plus an `aria-label` — layout the CALLER owns
 * through `className` and its own markup, not paint this catalog can pick. The
 * recipes stay layout-neutral (`min-w-0` everywhere, no fixed widths) precisely
 * so a caller can flip between the two; the ExpandedSidebar and CollapsedIconRail
 * stories show both being driven from one component tree.
 */

/**
 * The navigation landmark — Base UI's `NavigationMenu.Root`, a `<nav>` (or a
 * `<div data-nested>` when nested). `min-w-0` is the whole reason this recipe is
 * not empty: without it a `w-16` icon rail is blown open by its own labels.
 */
export const navigationMenuRootRecipe = cva("flex min-w-0");

/** The root recipe's variant props, mixed into `NavigationMenuRootProps`. */
export type NavigationMenuRootRecipeProps = VariantProps<typeof navigationMenuRootRecipe>;

/**
 * The row container — Base UI's `NavigationMenu.List`, a real `<ul>`. Its
 * margin/padding reset is load-bearing, not decoration: a browser's default
 * `padding-inline-start` of 40px would push every row off a 64px rail before any
 * of the catalog's own spacing applied.
 */
export const navigationMenuListRecipe = cva("m-0 flex min-w-0 list-none gap-1 p-0");

/** The list recipe's variant props, mixed into `NavigationMenuListProps`. */
export type NavigationMenuListRecipeProps = VariantProps<typeof navigationMenuListRecipe>;

/**
 * One row's wrapper — Base UI's `NavigationMenu.Item`, an `<li>`. It carries its
 * own marker reset rather than relying on the list's, because an Item is
 * legitimately rendered outside a List through the `render` prop.
 */
export const navigationMenuItemRecipe = cva("min-w-0 list-none");

/** The item recipe's variant props, mixed into `NavigationMenuItemProps`. */
export type NavigationMenuItemRecipeProps = VariantProps<typeof navigationMenuItemRecipe>;

/**
 * The row that opens a panel — Base UI's `NavigationMenu.Trigger`, a `<button>`.
 *
 * DELIBERATELY NOT COLOURLESS, unlike `dialogTriggerRecipe` and
 * `menuTriggerRecipe`. Those stay bare because a dialog or menu trigger is
 * routinely composed onto a real `Button`. A navigation trigger is not a button
 * in a toolbar — it is a NAV ROW that happens to open a panel, and it has to sit
 * flush beside the `Link` rows in the same list, so it takes the shared row shape
 * and states its own rest/hover/open colour. A caller who does want it
 * colourless overrides through `className`.
 */
export const navigationMenuTriggerRecipe = cva(
  "flex min-w-0 items-center gap-3 rounded-md px-3 py-2 text-sm font-medium whitespace-nowrap outline-none transition-colors aria-disabled:opacity-50",
  {
    variants: {
      /*
       * The same axis, spelled the same way, as `navigationMenuLinkRecipe`
       * below — read that recipe's comment for why a surface is a cva variant
       * and why no colour may stay in the base string. The one difference is
       * the state each arm's active pair keys off: a trigger's active state is
       * Base UI's `data-popup-open`, where a link's is `data-active`.
       *
       * REST TEXT is named here even on the `page` arm, which the trigger did
       * not do before. It is not decoration: a `<button>` does not inherit an
       * ancestor's `text-*` the way its sibling `<a>` does, so an unnamed rest
       * text painted the UA's black — measured ~1.1:1 against a real
       * `bg-sidebar` rail. An arm can only hand the rail a legible row by
       * owning the dimension, and once one arm owns it both must.
       */
      surface: {
        page: "text-foreground hover:bg-accent hover:text-accent-foreground data-[popup-open]:bg-accent data-[popup-open]:text-accent-foreground",
        sidebar:
          "text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-foreground data-[popup-open]:bg-sidebar-accent data-[popup-open]:text-sidebar-foreground",
      },
    },
    defaultVariants: { surface: "page" },
  },
);

/** The trigger recipe's variant props, mixed into `NavigationMenuTriggerProps`. */
export type NavigationMenuTriggerRecipeProps = VariantProps<typeof navigationMenuTriggerRecipe>;

/**
 * The chevron that says a row opens a panel — Base UI's `NavigationMenu.Icon`, a
 * `<span aria-hidden>` that gains `data-popup-open` in step with its trigger, so
 * the rotation is a modifier rather than a React prop.
 */
export const navigationMenuIconRecipe = cva(
  "ml-auto flex size-4 shrink-0 items-center justify-center transition-transform duration-150 data-[popup-open]:rotate-180",
);

/** The icon recipe's variant props, mixed into `NavigationMenuIconProps`. */
export type NavigationMenuIconRecipeProps = VariantProps<typeof navigationMenuIconRecipe>;

/**
 * One item's panel — Base UI's `NavigationMenu.Content`, a `<div>` that Base UI
 * MOVES into the shared popup's viewport while its item is active. The panel's
 * padding lives here rather than on the popup, because one popup serves every
 * item while each content is its own panel.
 */
export const navigationMenuContentRecipe = cva("flex min-w-0 flex-col gap-1 p-2");

/** The content recipe's variant props, mixed into `NavigationMenuContentProps`. */
export type NavigationMenuContentRecipeProps = VariantProps<typeof navigationMenuContentRecipe>;

/**
 * A navigating row — Base UI's `NavigationMenu.Link`, an `<a>`. Shares the
 * trigger's row shape so a list of links and a list of triggers line up. Base UI
 * puts `data-active` and `aria-current="page"` on a link marked `active`, so the
 * current-page treatment is a `data-[active]:` modifier.
 */
export const navigationMenuLinkRecipe = cva(
  "flex min-w-0 items-center gap-3 rounded-md px-3 py-2 text-sm font-medium whitespace-nowrap no-underline outline-none transition-colors",
  {
    variants: {
      /*
       * WHICH SURFACE the row is composed ONTO — the one axis of a nav row that
       * is not a Base UI state, so it is a cva variant rather than a `data-*`
       * modifier. A rail is an INVERTED surface, and a row that paints from the
       * page palette on it is either illegible or forces its consumer to
       * out-merge the recipe class by class (which is how a 1.27:1 hover
       * contrast defect shipped through a green suite).
       *
       * Every colour this recipe contributes therefore belongs to one arm or
       * the other, and none to the base string: an arm can only override what
       * tailwind-merge sees as the SAME utility at the SAME variant, so a colour
       * left in the base is a colour the sidebar arm cannot take back.
       *
       * The two arms are the SAME three states — rest, hover, current page —
       * spelled in the two palettes. On the rail all three collapse onto the one
       * `sidebar-accent` the theme ships, because that is the only surface the
       * inverted family names; the row still tells its states apart, since a
       * row at rest carries no surface at all.
       */
      surface: {
        page: "text-foreground hover:bg-accent hover:text-accent-foreground data-[active]:bg-accent data-[active]:text-accent-foreground",
        sidebar:
          "text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-foreground data-[active]:bg-sidebar-accent data-[active]:text-sidebar-foreground",
      },
    },
    defaultVariants: { surface: "page" },
  },
);

/** The link recipe's variant props, mixed into `NavigationMenuLinkProps`. */
export type NavigationMenuLinkRecipeProps = VariantProps<typeof navigationMenuLinkRecipe>;

/**
 * The outside-press catcher behind an open panel — Base UI's
 * `NavigationMenu.Backdrop`, a `<div role="presentation">`. Measured: Base UI
 * gives this element no inline positioning at all (only `user-select`), so
 * covering the window is entirely the recipe's job — same finding as
 * `menuBackdropRecipe`.
 *
 * Deliberately NOT a scrim: a desktop navigation bar must not dim the page
 * behind its own dropdown. The mobile-overlay presentation adds a translucent
 * background through `className` (see the MobileOverlay story).
 */
export const navigationMenuBackdropRecipe = cva("fixed inset-0");

/** The backdrop recipe's variant props, mixed into `NavigationMenuBackdropProps`. */
export type NavigationMenuBackdropRecipeProps = VariantProps<typeof navigationMenuBackdropRecipe>;

/**
 * The anchored wrapper Base UI positions against the active trigger —
 * `NavigationMenu.Positioner`. It owns the inline `position`/`left`/`top`
 * styles, so this recipe may only add stacking and focus concerns, never layout
 * that would fight the positioning engine. Same rule as `menuPositionerRecipe`.
 */
export const navigationMenuPositionerRecipe = cva("z-50 outline-none");

/** The positioner recipe's variant props, mixed into `NavigationMenuPositionerProps`. */
export type NavigationMenuPositionerRecipeProps = VariantProps<
  typeof navigationMenuPositionerRecipe
>;

/**
 * The shared card every item's panel appears inside — Base UI's
 * `NavigationMenu.Popup`, a `<nav tabindex="-1">`. Carries `relative` so
 * `NavigationMenu.Arrow`, which Base UI positions absolutely with an inline
 * `left` but no `top`, has this box as its containing block. No padding here:
 * the content owns that.
 */
export const navigationMenuPopupRecipe = cva(
  "relative rounded-md border border-border bg-popover text-popover-foreground shadow-md outline-none transition-all duration-150 data-[starting-style]:scale-95 data-[starting-style]:opacity-0 data-[ending-style]:scale-95 data-[ending-style]:opacity-0",
);

/** The popup recipe's variant props, mixed into `NavigationMenuPopupProps`. */
export type NavigationMenuPopupRecipeProps = VariantProps<typeof navigationMenuPopupRecipe>;

/**
 * The little pointer between the trigger and the popup — Base UI's
 * `NavigationMenu.Arrow`, a `<div aria-hidden>`. Identical to `menuArrowRecipe`
 * because both parts run on Base UI's one `useAnchorPositioning` engine and
 * share its `Side` vocabulary.
 */
export const navigationMenuArrowRecipe = cva(
  "size-2.5 rotate-45 rounded-sm border border-border bg-popover data-[side=bottom]:-top-1 data-[side=top]:-bottom-1 data-[side=inline-start]:-right-1 data-[side=inline-end]:-left-1",
);

/** The arrow recipe's variant props, mixed into `NavigationMenuArrowProps`. */
export type NavigationMenuArrowRecipeProps = VariantProps<typeof navigationMenuArrowRecipe>;

/**
 * The clipping container the active panel is moved into — Base UI's
 * `NavigationMenu.Viewport`, a `<div>`. Unlike Menu's viewport this one is not
 * optional in practice: one popup is shared by every item, so this is where the
 * panels cross-fade.
 */
export const navigationMenuViewportRecipe = cva("relative overflow-hidden");

/** The viewport recipe's variant props, mixed into `NavigationMenuViewportProps`. */
export type NavigationMenuViewportRecipeProps = VariantProps<typeof navigationMenuViewportRecipe>;
