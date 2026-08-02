import { cva, type VariantProps } from "class-variance-authority";

/**
 * The button's class recipe. Style decisions live here and nowhere else — this
 * file holds no JSX and imports no React, so a recipe can be read (and diffed)
 * without the component around it.
 *
 * Every utility is a semantic token class from `@bc-solutions-coder/styles`; no
 * raw colour values. The disabled treatment hangs off Base UI's `data-disabled`
 * state attribute rather than the `:disabled` pseudo-class, so it still applies
 * when the caller composes the button onto a non-button element via `render`.
 *
 * Four variant groups, and each one's DEFAULT reproduces the pre-upgrade button
 * exactly: `size="md"` owns the old base string's `px-3 py-2 text-sm`,
 * `width="full"` owns its `w-full`, `shape="rounded"` owns its `rounded-md`.
 * That is why those utilities left the base string — a non-full or pill button
 * must not have to fight a base class that tailwind-merge only sometimes wins.
 *
 * The focus indicator follows the catalog's existing form (`toolbar`,
 * `menubar`): `outline-none` plus a `focus-visible:` ring on the `ring` token.
 * The colour transition is `motion-safe:`-gated so a reader who asks for
 * reduced motion gets the state change without the animation.
 */
export const buttonRecipe = cva(
  "inline-flex items-center justify-center font-medium outline-none motion-safe:transition-colors focus-visible:ring-2 focus-visible:ring-ring data-[disabled]:opacity-50",
  {
    variants: {
      variant: {
        // The three solid variants each darken their OWN surface on hover. A
        // shared `hover:bg-accent` would hover a destructive button into a
        // neutral surface. There is no `*-hover` token in the theme, so the
        // treatment is an opacity suffix on the variant's own token.
        primary: "bg-primary text-primary-foreground hover:bg-primary/90",
        secondary: "bg-secondary text-secondary-foreground hover:bg-secondary/80",
        destructive: "bg-destructive text-destructive-foreground hover:bg-destructive/90",
        // The three quiet variants are told apart by what they DON'T draw at
        // rest: outline is a border with no surface, ghost is neither until
        // hover, link is underlined text with no box at all.
        //
        // `link`'s hover is the underline ALONE. The opacity suffix the solid
        // variants use to darken their own surface has no text-side equivalent a
        // fork can reach: a `hover:text-primary/80` is a colour `branding.json`
        // cannot name, and two call sites reaching for the same alpha have agreed
        // on a meaning nobody wrote down. That is what `wallow/no-tinted-text`
        // bans, and the catalog is the one place the decision is made — so the
        // affordance is the underline appearing, not the ink shifting.
        outline:
          "border border-border bg-transparent text-foreground hover:bg-accent hover:text-accent-foreground",
        ghost: "text-foreground hover:bg-accent hover:text-accent-foreground",
        link: "text-primary underline-offset-4 hover:underline",
      },
      size: {
        sm: "px-2.5 py-1.5 text-xs",
        md: "px-3 py-2 text-sm",
        lg: "px-5 py-2.5 text-base",
        // An icon-only button is a square target, not a text button with a
        // glyph in it — hence `size-*` and no horizontal text padding.
        icon: "size-9 p-0 text-sm",
      },
      width: {
        auto: "",
        full: "w-full",
      },
      shape: {
        rounded: "rounded-md",
        pill: "rounded-full",
      },
      // WHICH SURFACE the button is composed ONTO, which the `variant` axis
      // cannot express: `variant` says what KIND of button this is, and every
      // arm of it paints from the PAGE palette. Dropped onto the inverted
      // sidebar surface, a `secondary` button is a light chip on a dark rail.
      //
      // Declared LAST so its utilities land after `variant`'s in the cva output
      // and `cn()`'s tailwind-merge collapses the pair in this axis's favour —
      // that ordering is the whole mechanism, so it must not be reshuffled.
      //
      // The `sidebar` arm restates every colour dimension a `variant` arm can
      // set (rest surface, rest text, border, hover surface, hover text), since
      // tailwind-merge only drops the class a caller CONFLICTS with: a dimension
      // left unnamed here is a page colour that survives onto the rail. It is
      // the same rest/hover shape the rail's rows wear — no surface at rest, the
      // one `sidebar-accent` on hover — so a button reads as part of the rail
      // rather than as a chip dropped onto it.
      surface: {
        page: "",
        sidebar:
          "border-sidebar-accent bg-sidebar text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-foreground",
      },
    },
    compoundVariants: [
      // `width` defaults to `full` for the 11 pre-existing call sites' sake, so
      // a bare `<Button size="icon">` would otherwise stretch its square box.
      // The pair collapses through tailwind-merge, leaving `w-auto`.
      { size: "icon", width: "full", class: "w-auto" },
    ],
    defaultVariants: {
      variant: "primary",
      size: "md",
      width: "full",
      shape: "rounded",
      surface: "page",
    },
  },
);

/** The recipe's variant props, mixed into `ButtonProps`. */
export type ButtonRecipeProps = VariantProps<typeof buttonRecipe>;
