import { cva, type VariantProps } from "class-variance-authority";

/*
 * TWO recipes are the whole of this file, and the second one is not a part —
 * it is a class list meant to be handed to somebody else's part.
 *
 * `@base-ui/react/menubar` publishes exactly ONE export, the `Menubar` element
 * itself: a menubar has no dropdown anatomy of its own, it is a strip that a
 * row of ordinary `Menu.Root`s live inside. So the menus a bar contains are the
 * `Menu` component's own already-wrapped, already-styled parts (see menubar.tsx
 * for why that reuse goes the same way it did for Context Menu), and the only
 * element this component renders is the strip.
 *
 * The trigger is the one place where reuse alone is not enough. A menubar
 * trigger is genuinely different chrome from a standalone menu button: it sits
 * shoulder to shoulder with its neighbours inside a bordered strip, so it wants
 * its own padding and its own open-state highlight. `menuTriggerRecipe` is
 * deliberately colourless and padding-free (a standalone trigger is routinely
 * composed onto a real `Button`), which leaves exactly that delta for this
 * component to add — as a class list a caller passes to `Menu.Trigger`, not as
 * a second wrapper around the same Base UI part.
 */

/**
 * The strip itself — Base UI's `Menubar`, a `<div role="menubar">`.
 *
 * Orientation is styled from Base UI's own `data-orientation` attribute rather
 * than through a cva variant, which is the opposite of `radioGroupRecipe`'s
 * choice and deliberate: `orientation` is a real Base UI prop here (it drives
 * the composite's arrow-key axis and `aria-orientation`), so it must be
 * forwarded to the element anyway. Reading it back off the DOM keeps the
 * painting and the behaviour from ever disagreeing, and leaves `MenubarProps`
 * exactly Base UI's prop set with nothing to reconcile.
 *
 * `w-fit` so the bar hugs its menus instead of stretching across the page; a
 * caller who wants a full-width application bar passes `className="w-full"`,
 * which `cn()` resolves in their favour.
 */
export const menubarRecipe = cva(
  "flex w-fit gap-1 rounded-md border border-border bg-background p-1 text-foreground shadow-sm data-[orientation=horizontal]:flex-row data-[orientation=horizontal]:items-center data-[orientation=vertical]:flex-col data-[orientation=vertical]:items-stretch",
);

/** The bar recipe's variant props, mixed into `MenubarProps`. */
export type MenubarRecipeProps = VariantProps<typeof menubarRecipe>;

/**
 * What a menubar's `Menu.Trigger` adds on top of `menuTriggerRecipe` — pass it
 * as the trigger's `className`:
 *
 * ```tsx
 * <Menu.Trigger className={menubarTriggerRecipe()}>File</Menu.Trigger>
 * ```
 *
 * Every utility here is additive: `cn()` merges it with the trigger's own
 * recipe, so the shape (`inline-flex`, `rounded-md`, `text-sm font-medium`, the
 * disabled treatment) still comes from `Menu` and only the strip-specific
 * padding and the `data-[popup-open]:` highlight come from here. That is why
 * this is a class list rather than a `Menubar.Trigger` wrapper: a second
 * wrapper around the identical Base UI part is how two recipe sets start to
 * drift, and Base UI publishes no such part to mirror.
 */
export const menubarTriggerRecipe = cva(
  "px-3 py-1.5 outline-none focus-visible:ring-2 focus-visible:ring-ring data-[popup-open]:bg-accent data-[popup-open]:text-accent-foreground",
);

/** The trigger delta's variant props, exported for the catalog-wide shape. */
export type MenubarTriggerRecipeProps = VariantProps<typeof menubarTriggerRecipe>;
