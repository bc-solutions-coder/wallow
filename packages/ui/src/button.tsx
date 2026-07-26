import type { ButtonHTMLAttributes, ReactElement } from "react";

/**
 * The visual variants the shared button offers. `primary` is the measured
 * default (11x `w-full rounded-md bg-primary …` across wallow-auth); `secondary`
 * and `destructive` swap only the surface/foreground colour pair.
 */
export type ButtonVariant = "primary" | "secondary" | "destructive";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** Which colour recipe to render. Defaults to `primary`. */
  readonly variant?: ButtonVariant;
}

/**
 * The surface/foreground colour pair per variant, spliced into the shared recipe
 * at the exact positions the measured 11x `primary` string occupies so 6.5 can
 * refactor onto byte-identical rendered classes.
 */
const VARIANT_COLOURS: Record<ButtonVariant, { readonly surface: string; readonly text: string }> =
  {
    primary: { surface: "bg-primary", text: "text-primary-foreground" },
    secondary: { surface: "bg-secondary", text: "text-secondary-foreground" },
    destructive: { surface: "bg-destructive", text: "text-destructive-foreground" },
  };

/**
 * The shared button. Sourced from 11x
 * `w-full rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50`
 * in wallow-auth. All native button attributes (type, disabled, onClick,
 * data-testid) pass through; a caller `className` is appended to the recipe.
 */
export function Button({ variant = "primary", className, ...rest }: ButtonProps): ReactElement {
  const { surface, text } = VARIANT_COLOURS[variant];
  const recipe = `w-full rounded-md ${surface} px-3 py-2 text-sm font-medium ${text} disabled:opacity-50`;
  const merged = className === undefined ? recipe : `${recipe} ${className}`;

  return <button className={merged} {...rest} />;
}
