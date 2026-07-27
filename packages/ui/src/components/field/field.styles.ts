import { cva, type VariantProps } from "class-variance-authority";

import { inputRecipe } from "../input/input.styles";

/**
 * The Field anatomy's class recipes — one per styled part, JSX-free so the
 * styling can be read and diffed without the components around it.
 *
 * Base UI's Field is a CONTEXT: `Field.Root` publishes the field's state
 * (`disabled`, `invalid`, `touched`, `dirty`, `filled`, `focused`) as `data-*`
 * attributes onto every part beneath it. Every state treatment below therefore
 * hangs off a `data-[...]` selector rather than a CSS pseudo-class, which is
 * also what keeps it working when a caller substitutes the element via `render`.
 *
 * Every utility is a semantic token class from `@bc-solutions-coder/styles`.
 * None of these recipes has a variant axis today: the apps render one field
 * treatment, so a `size`/`tone` axis would have no consumer. The `VariantProps`
 * aliases stay in the components' prop types so the folder keeps the catalog's
 * uniform shape and a future variant is a one-file change.
 */

/**
 * The field row. `space-y-2` is the pre-rebuild `Field` recipe verbatim — the
 * compat guarantee for the 22 `<Field>` call sites across wallow-auth and
 * wallow-web, none of which may shift by a pixel.
 */
export const fieldRootRecipe = cva("space-y-2");

/** The recipe's variant props, mixed into `FieldProps`. */
export type FieldRootRecipeProps = VariantProps<typeof fieldRootRecipe>;

/**
 * The field label. `text-sm font-medium text-foreground` is the pre-rebuild
 * `Label` recipe verbatim (the compat guarantee for its 12 call sites); the
 * disabled treatment is new, and is what `Field.Root disabled` now makes
 * expressible.
 */
export const fieldLabelRecipe = cva(
  "text-sm font-medium text-foreground data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `LabelProps`. */
export type FieldLabelRecipeProps = VariantProps<typeof fieldLabelRecipe>;

/**
 * The field control. Base UI's `Input` IS `Field.Control` underneath, so this
 * deliberately composes `inputRecipe()` rather than restating it — the two must
 * never drift, because an app may use either inside a `Field`.
 *
 * The added `data-[invalid]:border-destructive` is the state treatment
 * Wallow-m5aq.2.2 explicitly deferred here: `data-invalid` only exists inside a
 * `Field.Root`, so it could not be styled — or tested — from the Input task.
 * It lives on this recipe rather than on `inputRecipe` so the Input task's
 * measured class set stays exactly as that bead pinned it.
 */
export const fieldControlRecipe = cva(`${inputRecipe()} data-[invalid]:border-destructive`);

/** The recipe's variant props, mixed into `FieldControlProps`. */
export type FieldControlRecipeProps = VariantProps<typeof fieldControlRecipe>;

/** The field's helper paragraph — the shared muted-body treatment. */
export const fieldDescriptionRecipe = cva("text-sm text-muted-foreground");

/** The recipe's variant props, mixed into `FieldDescriptionProps`. */
export type FieldDescriptionRecipeProps = VariantProps<typeof fieldDescriptionRecipe>;

/**
 * The field's validation message. Matches the destructive body text
 * `ErrorBanner` already renders, so a field-level and a form-level error read
 * as the same thing.
 */
export const fieldErrorRecipe = cva("text-sm text-destructive");

/** The recipe's variant props, mixed into `FieldErrorProps`. */
export type FieldErrorRecipeProps = VariantProps<typeof fieldErrorRecipe>;

/**
 * A single control grouped with its own label inside a field — Base UI's
 * `Field.Item`, used for the members of a checkbox or radio group. Those read
 * as a control beside its label, so this row is horizontal where the field row
 * above is vertical.
 */
export const fieldItemRecipe = cva("flex items-center gap-2");

/** The recipe's variant props, mixed into `FieldItemProps`. */
export type FieldItemRecipeProps = VariantProps<typeof fieldItemRecipe>;
