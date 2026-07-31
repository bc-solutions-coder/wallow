import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per styled part of the drawer. The class lists are not invented
 * here — drawer.test.tsx declares each part's exact utility set as a top-of-file
 * `*_CLASSES` constant and asserts it as an order-free set through the rendered
 * component, so that spec is the source of truth for everything below.
 *
 * A part gets a recipe only if it renders a VISIBLE element (the rule Dialog
 * established). `Root`, `Provider` and `VirtualKeyboardProvider` render no HTML
 * at all, `Portal` renders only the structural container Base UI appends to
 * `<body>`, and `Handle`/`createHandle` are the imperative API — none of them
 * has a recipe, and drawer.tsx re-exports all six unwrapped.
 *
 * THE ONE cva VARIANT: `side`. Unlike Dialog, a drawer has a real visual axis —
 * which screen edge it is anchored to — and it drives three parts at once
 * (`SwipeArea` sits on that edge, `Viewport` aligns the popup against it, and
 * `Popup` takes its shape, border, radius and enter/exit translate from it).
 * Everything else remains a STATE, published by Base UI as a `data-*` attribute
 * and pinned as a `data-[…]:` modifier in the base string rather than a variant.
 *
 * `side` mirrors Base UI's `Drawer.Root` `swipeDirection` prop, which names the
 * direction the drawer is swiped AWAY in: side `bottom` <-> `swipeDirection`
 * "down" (both defaults), `top` <-> "up", `left` <-> "left", `right` <->
 * "right". The two are deliberately separate — `swipeDirection` is Base UI's
 * gesture contract and `side` is this catalog's styling contract — so a caller
 * must set both, and every story below does.
 *
 * TAILWIND v4, LOAD-BEARING: `translate-*` and `scale-*` compile to the
 * INDIVIDUAL `translate` / `scale` CSS properties, not to a `transform`
 * function (verified by compiling these exact utilities through the repo's own
 * Tailwind 4.3.3). `transition-transform` still covers them — it emits
 * `transition-property: transform, translate, scale, rotate` — but nothing may
 * assert `getComputedStyle(...).transform` for a drawer; it is legitimately
 * "none" forever. See drawer.stories.tsx.
 */

/** The screen edge a drawer is anchored to. `bottom` is the default sheet. */
export type DrawerSide = "bottom" | "left" | "right" | "top";

/**
 * The button that opens the drawer — Base UI's `Drawer.Trigger`, a `<button>`.
 * Deliberately COLOURLESS, matching `dialogTriggerRecipe`: a trigger is
 * routinely composed onto a real `Button` through Base UI's `render` prop, and
 * a `bg-*` here would be merged away by tailwind-merge and silently beat the
 * Button's own background.
 */
export const drawerTriggerRecipe = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium transition-colors data-[disabled]:opacity-50",
);

/** The trigger recipe's variant props, mixed into `DrawerTriggerProps`. */
export type DrawerTriggerRecipeProps = VariantProps<typeof drawerTriggerRecipe>;

/**
 * The invisible edge strip that listens for an opening swipe — Base UI's
 * `Drawer.SwipeArea`, a `<div role="presentation" aria-hidden>`. It sits on the
 * SAME edge as the drawer, so it takes the same `side`; note that Base UI stamps
 * it with `data-swipe-direction` naming the direction you swipe TO OPEN, which
 * is the opposite of the popup's (measured: `side="right"` gives the popup
 * `data-swipe-direction="right"` and the swipe area `"left"`).
 *
 * It sits BELOW the backdrop's `z-50` on purpose: once the drawer is open the
 * strip has served its purpose, and Base UI already switches it to
 * `pointer-events: none` inline.
 */
export const drawerSwipeAreaRecipe = cva("fixed z-40", {
  variants: {
    side: {
      bottom: "inset-x-0 bottom-0 h-6",
      left: "inset-y-0 left-0 w-6",
      right: "inset-y-0 right-0 w-6",
      top: "inset-x-0 top-0 h-6",
    },
  },
  defaultVariants: { side: "bottom" },
});

/** The swipe area recipe's variant props, mixed into `DrawerSwipeAreaProps`. */
export type DrawerSwipeAreaRecipeProps = VariantProps<typeof drawerSwipeAreaRecipe>;

/**
 * The dimming scrim behind an open drawer — Base UI's `Drawer.Backdrop`, a
 * `<div>`. Side-agnostic: it covers the window whichever edge the drawer is on.
 * The two `data-[…-style]:opacity-0` modifiers are the ONLY place the enter and
 * exit phases are expressed.
 */
export const drawerBackdropRecipe = cva(
  "fixed inset-0 z-50 bg-foreground/50 transition-opacity duration-300 data-[starting-style]:opacity-0 data-[ending-style]:opacity-0",
);

/** The backdrop recipe's variant props, mixed into `DrawerBackdropProps`. */
export type DrawerBackdropRecipeProps = VariantProps<typeof drawerBackdropRecipe>;

/**
 * The positioning container around the popup — Base UI's `Drawer.Viewport`, a
 * `<div>`. Unlike `Dialog.Viewport` this part is effectively REQUIRED: Base UI
 * logs "expected to be rendered within <Drawer.Viewport>" and disables swipe
 * handling and touch scroll locking without it (measured). It owns the
 * anchoring outright — Base UI positions nothing itself, it only publishes the
 * swipe CSS variables — so the flex alignment here is what puts the popup on
 * the chosen edge.
 */
export const drawerViewportRecipe = cva("fixed inset-0 z-50 flex", {
  variants: {
    side: {
      bottom: "items-end justify-center",
      left: "items-stretch justify-start",
      right: "items-stretch justify-end",
      top: "items-start justify-center",
    },
  },
  defaultVariants: { side: "bottom" },
});

/** The viewport recipe's variant props, mixed into `DrawerViewportProps`. */
export type DrawerViewportRecipeProps = VariantProps<typeof drawerViewportRecipe>;

/**
 * The sliding panel itself — Base UI's `Drawer.Popup`, a `<div role="dialog">`.
 *
 * The panel is `relative` rather than `fixed`: the viewport is the fixed layer
 * and flex-aligns this against the chosen edge, which is what lets `side` change
 * the anchor without every part relearning its own inset.
 *
 * `translate-x-(--drawer-swipe-movement-x)` / `-y` are the live swipe follow:
 * Base UI writes those custom properties onto this element as the finger moves
 * (measured on the open popup), so the panel tracks the gesture, and
 * `data-[swiping]:duration-0` stops the transition from lagging behind it. The
 * `data-[starting-style]:` / `data-[ending-style]:` translates are the slide-in
 * and slide-out, off the same edge.
 */
export const drawerPopupRecipe = cva(
  "relative z-50 flex flex-col overflow-y-auto border-border bg-popover text-popover-foreground shadow-lg outline-none transition-transform duration-300 data-[swiping]:duration-0",
  {
    variants: {
      side: {
        bottom:
          "w-full max-h-[90vh] rounded-t-lg border-t translate-y-(--drawer-swipe-movement-y) data-[starting-style]:translate-y-full data-[ending-style]:translate-y-full",
        left: "h-full w-80 max-w-[90vw] rounded-r-lg border-r translate-x-(--drawer-swipe-movement-x) data-[starting-style]:-translate-x-full data-[ending-style]:-translate-x-full",
        right:
          "h-full w-80 max-w-[90vw] rounded-l-lg border-l translate-x-(--drawer-swipe-movement-x) data-[starting-style]:translate-x-full data-[ending-style]:translate-x-full",
        top: "w-full max-h-[90vh] rounded-b-lg border-b translate-y-(--drawer-swipe-movement-y) data-[starting-style]:-translate-y-full data-[ending-style]:-translate-y-full",
      },
    },
    defaultVariants: { side: "bottom" },
  },
);

/** The popup recipe's variant props, mixed into `DrawerPopupProps`. */
export type DrawerPopupRecipeProps = VariantProps<typeof drawerPopupRecipe>;

/**
 * The inner content box — Base UI's `Drawer.Content`, a
 * `<div data-drawer-content>`. It exists so text inside it can be selected with
 * a mouse without the selection being read as a swipe, and it is where the
 * padding lives: the popup itself stays padding-free so a caller can run an
 * image or a sticky header edge-to-edge beside a padded `Content`.
 */
export const drawerContentRecipe = cva("flex flex-col gap-2 p-6");

/** The content recipe's variant props, mixed into `DrawerContentProps`. */
export type DrawerContentRecipeProps = VariantProps<typeof drawerContentRecipe>;

/**
 * The heading that names the drawer — Base UI's `Drawer.Title`, an `<h2>`.
 * COLOURLESS on purpose, following `popoverTitleRecipe` rather than
 * `dialogTitleRecipe`: the popup already establishes `text-popover-foreground`,
 * and restating a page-level colour here would break a caller who recolours the
 * panel.
 */
export const drawerTitleRecipe = cva("text-xl font-semibold");

/** The title recipe's variant props, mixed into `DrawerTitleProps`. */
export type DrawerTitleRecipeProps = VariantProps<typeof drawerTitleRecipe>;

/** The supporting copy under the title — Base UI's `Drawer.Description`, a `<p>`. */
export const drawerDescriptionRecipe = cva("mt-1 text-sm text-muted-foreground");

/** The description recipe's variant props, mixed into `DrawerDescriptionProps`. */
export type DrawerDescriptionRecipeProps = VariantProps<typeof drawerDescriptionRecipe>;

/**
 * The button that dismisses the drawer — Base UI's `Drawer.Close`, a
 * `<button>`. Matches `dialogCloseRecipe`: no absolute corner positioning, so a
 * caller is free to put the close in a footer row or in the panel's corner.
 */
export const drawerCloseRecipe = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium text-muted-foreground transition-colors hover:text-foreground data-[disabled]:opacity-50",
);

/** The close recipe's variant props, mixed into `DrawerCloseProps`. */
export type DrawerCloseRecipeProps = VariantProps<typeof drawerCloseRecipe>;

/**
 * The wrapper around the app's own UI — Base UI's `Drawer.Indent`, a `<div>`.
 * Base UI stamps it `data-active` while any drawer under the nearest
 * `Drawer.Provider` is open and `data-inactive` otherwise (measured, both
 * states), which is the iOS-style "the page shrinks back" effect. Side-agnostic:
 * the scale reads the same from every edge.
 */
export const drawerIndentRecipe = cva(
  "origin-top transition-transform duration-300 data-[active]:scale-[0.97]",
);

/** The indent recipe's variant props, mixed into `DrawerIndentProps`. */
export type DrawerIndentRecipeProps = VariantProps<typeof drawerIndentRecipe>;

/**
 * The layer revealed behind the shrinking app UI — Base UI's
 * `Drawer.IndentBackground`, a `<div>`. It carries the same `data-active` /
 * `data-inactive` pair as the indent and fades in underneath it, so the gap the
 * scale opens up is filled rather than showing the page background.
 */
export const drawerIndentBackgroundRecipe = cva(
  "fixed inset-0 -z-10 bg-foreground opacity-0 transition-opacity duration-300 data-[active]:opacity-100",
);

/** The indent background recipe's variant props, mixed into `DrawerIndentBackgroundProps`. */
export type DrawerIndentBackgroundRecipeProps = VariantProps<typeof drawerIndentBackgroundRecipe>;
