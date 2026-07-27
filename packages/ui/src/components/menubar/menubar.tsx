import { Menubar as BaseMenubar } from "@base-ui/react/menubar";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { menubarRecipe, type MenubarRecipeProps } from "./menubar.styles";

/**
 * A menubar is the strip of menus along the top of an application — File, Edit,
 * View — and Base UI models it as exactly that and nothing more. Its subpath
 * publishes ONE export, the `Menubar` element, with no dropdown anatomy of its
 * own: the menus inside a bar are ordinary `Menu.Root`s, and Base UI's
 * `MenubarContext` reaches down into them so that a plain `Menu.Trigger`
 * becomes a `role="menuitem"` in the bar's roving tab order, and the whole row
 * behaves as one composite widget.
 *
 * So this component is a single component, not a namespace object, and the
 * catalog's `Menu` supplies everything below the strip:
 *
 * ```tsx
 * <Menubar>
 *   <Menu.Root>
 *     <Menu.Trigger className={menubarTriggerRecipe()}>File</Menu.Trigger>
 *     <Menu.Portal>
 *       <Menu.Positioner>
 *         <Menu.Popup>
 *           <Menu.Item>New file</Menu.Item>
 *         </Menu.Popup>
 *       </Menu.Positioner>
 *     </Menu.Portal>
 *   </Menu.Root>
 *   …one more `Menu.Root` per menu on the bar.
 * </Menubar>
 * ```
 *
 * That composition is the same reuse decision Context Menu made, arrived at
 * from the other direction. Context Menu re-exports `Menu`'s wrappers because
 * Base UI re-exports `menu`'s runtime on its own subpath; a menubar's menus are
 * not re-exported anywhere because they were never anything but menus. Either
 * way the catalog styles the menu card once, in `menu.styles.ts`, and a fork
 * that restyles it restyles every menu in the product. Inventing namespace keys
 * here — a `Menubar.Menu`, a `Menubar.Popup` — would mirror a Base UI surface
 * that does not exist and mint a second recipe set that could drift from
 * `Menu`'s.
 *
 * What the bar itself contributes, on top of laying its menus out in a strip:
 *
 *   - ONE ROVING TAB STOP for the whole row, so Tab enters and leaves the bar
 *     rather than walking through every menu name, and the arrow keys move
 *     along it (looping at the ends, with Home and End jumping to them);
 *   - ARROW KEYS THAT SWITCH THE OPEN MENU. With a menu down, the same key that
 *     roved focus now closes it and opens the neighbour — the behaviour that
 *     makes a bar a bar rather than three unrelated dropdowns;
 *   - `modal` and `disabled` for the whole row at once, and `orientation` for a
 *     vertical rail.
 */

/*
 * `className` is deliberately narrowed back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. Every component in this catalog makes
 * the same narrowing.
 */

/**
 * Every Base UI `Menubar` prop (`orientation`, `modal`, `disabled`,
 * `loopFocus`, `render` and the native div attributes), with `className`
 * narrowed to `string`.
 *
 * There is no recipe prop to pass: the bar's one styling axis is its
 * orientation, and the recipe reads that off Base UI's own `data-orientation`
 * attribute rather than taking it a second time (see menubar.styles.ts).
 */
export interface MenubarProps
  extends Omit<ComponentProps<typeof BaseMenubar>, "className">, MenubarRecipeProps {
  readonly className?: string;
}

/**
 * The catalog's menubar: the strip a row of `Menu.Root`s live in, and the
 * composite that makes them behave as one widget.
 */
export function Menubar({ className, ...rest }: MenubarProps): ReactElement {
  return <BaseMenubar className={cn(menubarRecipe(), className)} {...rest} />;
}
