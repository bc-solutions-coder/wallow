import type { HTMLAttributes, ReactElement } from "react";

/**
 * The label+control field row. Sourced from 16x `space-y-2` wrappers in
 * wallow-auth (each Label stacked over its Input). Children and data-testid
 * pass through onto the row `div`.
 */
export type FieldProps = HTMLAttributes<HTMLDivElement>;

export function Field(props: FieldProps): ReactElement {
  return <div {...props} className="space-y-2" />;
}
