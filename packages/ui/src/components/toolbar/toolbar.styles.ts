import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per part of the toolbar. The class lists are not invented here —
 * toolbar.test.tsx declares each part's exact utility set as a top-of-file
 * `*_CLASSES` constant and asserts it as an order-free set THROUGH the rendered
 * component, so that spec is the source of truth for everything below.
 *
 * All six Base UI parts render a visible element, so all six get a recipe.
 *
 * No recipe takes a cva VARIANT. The two axes a toolbar actually has — its
 * ORIENTATION and whether an item is DISABLED — are both published by Base UI as
 * `data-*` attributes and cascade from the root to every part, so they belong in
 * the base string as `data-[orientation=…]:` / `data-[disabled]:` modifiers
 * rather than as cva variants a caller would have to pass again on every child.
 * The `VariantProps` aliases are still exported so each part's props keep the
 * catalog-wide shape and a later variant stays a non-breaking addition.
 *
 * `data-[disabled]:` — never `:disabled` — is what dims a control here.
 * `focusableWhenDisabled` defaults to TRUE, so a disabled toolbar item carries
 * `aria-disabled` and NO native `disabled` attribute, and a recipe written
 * against the pseudo-class would silently paint nothing.
 */

/** The strip: a roving-focus container for the controls inside it. */
export const toolbarRootRecipe = cva(
  "flex items-center gap-1 rounded-md border border-border bg-card p-1 data-[orientation=vertical]:flex-col data-[orientation=vertical]:items-stretch data-[disabled]:opacity-50",
);

/** The root recipe's variant props, mixed into `ToolbarRootProps`. */
export type ToolbarRootRecipeProps = VariantProps<typeof toolbarRootRecipe>;

/**
 * A cluster of related controls inside the strip. Repeats the root's flex axis
 * because the group is a nested flex container of its own; it deliberately adds
 * no frame of its own, so a group reads as tighter spacing rather than a box
 * inside a box.
 */
export const toolbarGroupRecipe = cva(
  "flex items-center gap-1 data-[orientation=vertical]:flex-col data-[orientation=vertical]:items-stretch",
);

/** The group recipe's variant props, mixed into `ToolbarGroupProps`. */
export type ToolbarGroupRecipeProps = VariantProps<typeof toolbarGroupRecipe>;

/** An individual control. */
export const toolbarButtonRecipe = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-sm px-2.5 py-1.5 text-sm font-medium text-foreground outline-none transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:ring-2 focus-visible:ring-ring data-[disabled]:opacity-50",
);

/** The button recipe's variant props, mixed into `ToolbarButtonProps`. */
export type ToolbarButtonRecipeProps = VariantProps<typeof toolbarButtonRecipe>;

/**
 * A navigational item in the strip. Reads as a link rather than a button — it
 * takes the reader somewhere instead of acting on what is in front of them.
 */
export const toolbarLinkRecipe = cva(
  "inline-flex items-center gap-1 rounded-sm px-2.5 py-1.5 text-sm font-medium text-primary underline-offset-4 outline-none hover:underline focus-visible:ring-2 focus-visible:ring-ring",
);

/** The link recipe's variant props, mixed into `ToolbarLinkProps`. */
export type ToolbarLinkRecipeProps = VariantProps<typeof toolbarLinkRecipe>;

/** A text field that joins the strip's roving focus. */
export const toolbarInputRecipe = cva(
  "h-8 rounded-sm border border-border bg-background px-2 text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring data-[disabled]:opacity-50",
);

/** The input recipe's variant props, mixed into `ToolbarInputProps`. */
export type ToolbarInputRecipeProps = VariantProps<typeof toolbarInputRecipe>;

/**
 * The rule between two clusters. Distinct from the catalog's standalone
 * `Separator`: this one is a Base UI TOOLBAR part whose orientation defaults to
 * the OPPOSITE of the toolbar's, so the `data-[orientation=vertical]:` arm is
 * what fires in the everyday HORIZONTAL strip — the reverse of every other part
 * here.
 *
 * The vertical rule is a fixed `h-5` rather than `h-full`: the strip is
 * `items-center`, so a full-height rule has no height to stretch to and collapses
 * to nothing.
 */
export const toolbarSeparatorRecipe = cva(
  "shrink-0 bg-border data-[orientation=vertical]:h-5 data-[orientation=vertical]:w-px data-[orientation=horizontal]:h-px data-[orientation=horizontal]:w-full",
);

/** The separator recipe's variant props, mixed into `ToolbarSeparatorProps`. */
export type ToolbarSeparatorRecipeProps = VariantProps<typeof toolbarSeparatorRecipe>;
