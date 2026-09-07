import { useRender } from "@base-ui/react/use-render";
import type { ReactElement } from "react";

import { cn } from "../../core/cn";
import { listRowRecipe } from "./list-row.styles";

/**
 * List item attributes with an optional render element or function. render replaces the li and
 * receives merged classes, handlers, and the derived test ID.
 */
export type ListRowProps = useRender.ComponentProps<"li"> & {
  /**
   * The app's name for a row of this list. The row's test id is DERIVED from
   * it (`{name}-item`), and it survives onto whatever element `render`
   * substitutes, so an E2E selector keeps resolving when the row becomes a link.
   */
  readonly name: string;
};

/**
 * Renders a padded list row with hover and keyboard-focus styles. render can replace the li
 * with a link or another element; the row test ID is `${name}-item`.
 */
export function ListRow({ name, className, render, ref, ...rest }: ListRowProps): ReactElement {
  return useRender({
    render,
    ref,
    defaultTagName: "li",
    props: {
      ...rest,
      className: cn(listRowRecipe(), className),
      "data-testid": `${name}-item`,
    },
  });
}
