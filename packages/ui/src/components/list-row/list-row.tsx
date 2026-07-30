import { useRender } from "@base-ui/react/use-render";
import type { ReactElement } from "react";

import { cn } from "../../core/cn";
import { listRowRecipe } from "./list-row.styles";

/**
 * A single row inside a `ListCard`: the `<li>` cell every wallow-web list page
 * hand-rolls today, and — through `render` — the element a caller composes a
 * TanStack Router `Link` onto so the whole row navigates.
 *
 * `ListRow` wraps no headless Base UI part, so it takes the `render` contract
 * from `@base-ui/react`'s own `useRender` hook rather than inventing a second
 * spelling of it: `render` accepts a `ReactElement` or a function, and the
 * substituted element receives the recipe, the derived test id and the rest
 * props with its own className and event handlers merged in rather than
 * replaced. `render` SUBSTITUTES the element, it does not wrap it — so a
 * composed row is the anchor itself, which is what makes the whole row the
 * navigation target.
 */
export type ListRowProps = useRender.ComponentProps<"li"> & {
  /**
   * The app's name for a row of this list. The row's test id is DERIVED from
   * it (`{name}-item`), and it survives onto whatever element `render`
   * substitutes, so an E2E selector keeps resolving when the row becomes a link.
   */
  readonly name: string;
};

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
