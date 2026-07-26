import type { InputHTMLAttributes, ReactElement } from "react";

/**
 * The shared text input. Sourced from 10x
 * `w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground`
 * in wallow-auth (EmailField/PasswordField and siblings). All native input
 * attributes (type, value, onChange, placeholder, id, data-testid) pass through;
 * a caller `className` is appended to the recipe.
 */
export type InputProps = InputHTMLAttributes<HTMLInputElement>;

export function Input({ className, ...rest }: InputProps): ReactElement {
  const recipe =
    "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground";
  const merged = className === undefined ? recipe : `${recipe} ${className}`;

  return <input className={merged} {...rest} />;
}
