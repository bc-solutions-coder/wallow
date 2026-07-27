import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per styled part of the toast. The class lists are not invented
 * here — toast.test.tsx declares each part's exact utility set as a top-of-file
 * `*_CLASSES` constant and asserts it as an order-free set through the rendered
 * component, so that spec is the source of truth for everything below.
 *
 * One recipe per part that renders a VISIBLE element (the Wallow-m5aq.3.1
 * Dialog rule). `Provider` renders no element, `Portal` renders only the
 * structural container Base UI appends to `<body>`, and `useToastManager` /
 * `createToastManager` are not components at all, so none of the four has a
 * recipe — see toast.tsx for why they are re-exported unwrapped.
 *
 * No recipe takes a cva VARIANT, and for Toast that is not merely a convention:
 * a toast's `type` (`success`, `error`, the `loading`/`success`/`error` a
 * promise toast walks through) is set on the TOAST OBJECT by the manager, never
 * passed to `Toast.Root` by the caller, so it can only be reached as the
 * `data-type` attribute Base UI stamps on the root. The same holds for
 * `expanded`, `limited` and the transition phases. They all live in the base
 * string as `data-[…]:` modifiers. The `VariantProps` types are still exported
 * so each part's props keep the catalog-wide shape and a later variant axis
 * stays a non-breaking addition.
 */

/**
 * The corner stack of toasts — Base UI's `Toast.Viewport`, a
 * `<div role="region">`. Base UI positions NOTHING for a stacked toast (measured:
 * the viewport's only inline style is the `--toast-frontmost-height` custom
 * property), so — like `dialogPopupRecipe` and unlike every anchored overlay —
 * this recipe owns the placement outright.
 */
export const toastViewportRecipe = cva(
  "fixed right-4 bottom-4 z-50 flex w-80 flex-col gap-2 outline-none",
);

/** The viewport recipe's variant props, mixed into `ToastViewportProps`. */
export type ToastViewportRecipeProps = VariantProps<typeof toastViewportRecipe>;

/**
 * One toast card — Base UI's `Toast.Root`, a `<div role="dialog">`. Three of the
 * modifiers pin states no caller can pass as a prop: `data-limited` is stamped on
 * the toasts pushed past the provider's `limit` (Base UI keeps them mounted so
 * they can animate out rather than vanishing), and `data-type` is stamped from
 * the toast object, which is how a promise toast reports failure. Only `error`
 * gets a colour — `@bc-solutions-coder/styles` publishes no success token.
 */
export const toastRootRecipe = cva(
  "relative flex w-full flex-col gap-2 rounded-lg border border-border bg-popover p-4 text-popover-foreground shadow-lg outline-none transition-all duration-150 data-[starting-style]:translate-y-2 data-[starting-style]:opacity-0 data-[ending-style]:translate-y-2 data-[ending-style]:opacity-0 data-[limited]:opacity-0 data-[type=error]:border-destructive",
);

/** The root recipe's variant props, mixed into `ToastRootProps`. */
export type ToastRootRecipeProps = VariantProps<typeof toastRootRecipe>;

/** The title/description column — Base UI's `Toast.Content`, a `<div>`. */
export const toastContentRecipe = cva("flex min-w-0 flex-1 flex-col gap-1");

/** The content recipe's variant props, mixed into `ToastContentProps`. */
export type ToastContentRecipeProps = VariantProps<typeof toastContentRecipe>;

/** The toast's heading — Base UI's `Toast.Title`, an `<h2>`. */
export const toastTitleRecipe = cva("text-sm font-semibold text-foreground");

/** The title recipe's variant props, mixed into `ToastTitleProps`. */
export type ToastTitleRecipeProps = VariantProps<typeof toastTitleRecipe>;

/** The toast's message — Base UI's `Toast.Description`, a `<p>`. */
export const toastDescriptionRecipe = cva("text-sm text-muted-foreground");

/** The description recipe's variant props, mixed into `ToastDescriptionProps`. */
export type ToastDescriptionRecipeProps = VariantProps<typeof toastDescriptionRecipe>;

/**
 * The toast's action button — Base UI's `Toast.Action`, a `<button>`. Reads as a
 * link rather than a filled button so it never competes with the page's own
 * primary action. Deliberately COLOURLESS, for the reason `dialogTriggerRecipe`
 * records: an action is routinely composed onto a real `Button` through Base
 * UI's `render` prop, and a colour here would be merged away by tailwind-merge
 * and silently beat that button's own paint.
 */
export const toastActionRecipe = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium transition-colors hover:underline data-[disabled]:opacity-50",
);

/** The action recipe's variant props, mixed into `ToastActionProps`. */
export type ToastActionRecipeProps = VariantProps<typeof toastActionRecipe>;

/** The toast's dismiss button — Base UI's `Toast.Close`, a `<button>`. */
export const toastCloseRecipe = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium text-muted-foreground transition-colors hover:text-foreground data-[disabled]:opacity-50",
);

/** The close recipe's variant props, mixed into `ToastCloseProps`. */
export type ToastCloseRecipeProps = VariantProps<typeof toastCloseRecipe>;

/**
 * An anchored toast's positioner — Base UI's `Toast.Positioner`, a `<div>`. An
 * anchored toast is the one shape where Base UI owns the placement itself
 * (measured inline: `position:absolute`, `left`, `top`, a `transform` and the
 * `--available-*` / `--anchor-*` custom properties), so this recipe may only add
 * stacking and focus — the rule `selectPositionerRecipe` established. Do not
 * copy the placement utilities from `toastViewportRecipe` down here.
 */
export const toastPositionerRecipe = cva("z-50 outline-none");

/** The positioner recipe's variant props, mixed into `ToastPositionerProps`. */
export type ToastPositionerRecipeProps = VariantProps<typeof toastPositionerRecipe>;

/**
 * An anchored toast's arrow — Base UI's `Toast.Arrow`, a `<div>`. Base UI places
 * it with an inline `position:absolute` plus a side-dependent axis, so this
 * recipe is size and paint only.
 */
export const toastArrowRecipe = cva("size-2 rotate-45 rounded-sm border border-border bg-popover");

/** The arrow recipe's variant props, mixed into `ToastArrowProps`. */
export type ToastArrowRecipeProps = VariantProps<typeof toastArrowRecipe>;
