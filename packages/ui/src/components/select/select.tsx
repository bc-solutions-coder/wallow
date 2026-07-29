import { Select as BaseSelect } from "@base-ui/react/select";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  selectArrowRecipe,
  type SelectArrowRecipeProps,
  selectBackdropRecipe,
  type SelectBackdropRecipeProps,
  selectGroupLabelRecipe,
  type SelectGroupLabelRecipeProps,
  selectGroupRecipe,
  type SelectGroupRecipeProps,
  selectIconRecipe,
  type SelectIconRecipeProps,
  selectItemIndicatorRecipe,
  type SelectItemIndicatorRecipeProps,
  selectItemRecipe,
  type SelectItemRecipeProps,
  selectItemTextRecipe,
  type SelectItemTextRecipeProps,
  selectLabelRecipe,
  type SelectLabelRecipeProps,
  selectListRecipe,
  type SelectListRecipeProps,
  selectPopupRecipe,
  type SelectPopupRecipeProps,
  selectPositionerRecipe,
  type SelectPositionerRecipeProps,
  selectScrollArrowRecipe,
  type SelectScrollArrowRecipeProps,
  selectSeparatorRecipe,
  type SelectSeparatorRecipeProps,
  selectTriggerRecipe,
  type SelectTriggerRecipeProps,
  selectValueRecipe,
  type SelectValueRecipeProps,
} from "./select.styles";

/**
 * Two of Base UI's nineteen parts are re-exported UNWRAPPED, because neither can
 * carry a recipe:
 *
 *   - `Root` renders no HTML element at all (it is the state container), and it
 *     is generic over the item value type — wrapping it would either drop the
 *     generic or add an element the DOM does not want.
 *   - `Portal` takes Floating UI's portal props (`container`, `keepMounted`) and
 *     has no `className` to merge in the first place.
 *
 * Every other part is wrapped so its recipe travels with it.
 */

/** Every Base UI `Select.Root` prop, generic over the item value type. */
export type SelectRootProps<
  Value = string,
  Multiple extends boolean | undefined = false,
> = Parameters<typeof BaseSelect.Root<Value, Multiple>>[0];

/** Every Base UI `Select.Portal` prop. This part takes no `className`. */
export type SelectPortalProps = ComponentProps<typeof BaseSelect.Portal>;

/*
 * `className` is deliberately narrowed back to `string` on every wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
 */

/** Every Base UI `Select.Label` prop, with `className` narrowed to `string`. */
export interface SelectLabelProps
  extends Omit<ComponentProps<typeof BaseSelect.Label>, "className">, SelectLabelRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.Trigger` prop, with `className` narrowed to `string`. */
export interface SelectTriggerProps
  extends Omit<ComponentProps<typeof BaseSelect.Trigger>, "className">, SelectTriggerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.Value` prop, with `className` narrowed to `string`. */
export interface SelectValueProps
  extends Omit<ComponentProps<typeof BaseSelect.Value>, "className">, SelectValueRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.Icon` prop, with `className` narrowed to `string`. */
export interface SelectIconProps
  extends Omit<ComponentProps<typeof BaseSelect.Icon>, "className">, SelectIconRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.Backdrop` prop, with `className` narrowed to `string`. */
export interface SelectBackdropProps
  extends Omit<ComponentProps<typeof BaseSelect.Backdrop>, "className">, SelectBackdropRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.Positioner` prop, with `className` narrowed to `string`. */
export interface SelectPositionerProps
  extends
    Omit<ComponentProps<typeof BaseSelect.Positioner>, "className">,
    SelectPositionerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.Popup` prop, with `className` narrowed to `string`. */
export interface SelectPopupProps
  extends Omit<ComponentProps<typeof BaseSelect.Popup>, "className">, SelectPopupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.List` prop, with `className` narrowed to `string`. */
export interface SelectListProps
  extends Omit<ComponentProps<typeof BaseSelect.List>, "className">, SelectListRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.Item` prop, with `className` narrowed to `string`. */
export interface SelectItemProps
  extends Omit<ComponentProps<typeof BaseSelect.Item>, "className">, SelectItemRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.ItemText` prop, with `className` narrowed to `string`. */
export interface SelectItemTextProps
  extends Omit<ComponentProps<typeof BaseSelect.ItemText>, "className">, SelectItemTextRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.ItemIndicator` prop, with `className` narrowed to `string`. */
export interface SelectItemIndicatorProps
  extends
    Omit<ComponentProps<typeof BaseSelect.ItemIndicator>, "className">,
    SelectItemIndicatorRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.Arrow` prop, with `className` narrowed to `string`. */
export interface SelectArrowProps
  extends Omit<ComponentProps<typeof BaseSelect.Arrow>, "className">, SelectArrowRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.ScrollUpArrow` prop, with `className` narrowed to `string`. */
export interface SelectScrollUpArrowProps
  extends
    Omit<ComponentProps<typeof BaseSelect.ScrollUpArrow>, "className">,
    SelectScrollArrowRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.ScrollDownArrow` prop, with `className` narrowed to `string`. */
export interface SelectScrollDownArrowProps
  extends
    Omit<ComponentProps<typeof BaseSelect.ScrollDownArrow>, "className">,
    SelectScrollArrowRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.Group` prop, with `className` narrowed to `string`. */
export interface SelectGroupProps
  extends Omit<ComponentProps<typeof BaseSelect.Group>, "className">, SelectGroupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.GroupLabel` prop, with `className` narrowed to `string`. */
export interface SelectGroupLabelProps
  extends
    Omit<ComponentProps<typeof BaseSelect.GroupLabel>, "className">,
    SelectGroupLabelRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Select.Separator` prop, with `className` narrowed to `string`. */
export interface SelectSeparatorProps
  extends
    Omit<ComponentProps<typeof BaseSelect.Separator>, "className">,
    SelectSeparatorRecipeProps {
  readonly className?: string;
}

function SelectLabel({ className, ...rest }: SelectLabelProps): ReactElement {
  return <BaseSelect.Label className={cn(selectLabelRecipe(), className)} {...rest} />;
}

function SelectTrigger({ className, ...rest }: SelectTriggerProps): ReactElement {
  return <BaseSelect.Trigger className={cn(selectTriggerRecipe(), className)} {...rest} />;
}

function SelectValue({ className, ...rest }: SelectValueProps): ReactElement {
  return <BaseSelect.Value className={cn(selectValueRecipe(), className)} {...rest} />;
}

/**
 * The chevron `Select.Icon` falls back to when a caller passes no children.
 *
 * Inline SVG on purpose: this package ships no icon library and must not gain
 * one, and the text glyph call sites used to pass sits off the baseline of the
 * `size-4` icon box and renders differently on every platform. `currentColor`
 * and the plain `size-4` box let the icon recipe keep driving both the colour
 * and the `data-[popup-open]:rotate-180` flip.
 */
function DefaultChevron(): ReactElement {
  return (
    <svg
      className="size-4"
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="2"
      viewBox="0 0 24 24"
    >
      <path d="m6 9 6 6 6-6" />
    </svg>
  );
}

function SelectIcon({ className, children, ...rest }: SelectIconProps): ReactElement {
  return (
    <BaseSelect.Icon className={cn(selectIconRecipe(), className)} {...rest}>
      {children ?? <DefaultChevron />}
    </BaseSelect.Icon>
  );
}

function SelectBackdrop({ className, ...rest }: SelectBackdropProps): ReactElement {
  return <BaseSelect.Backdrop className={cn(selectBackdropRecipe(), className)} {...rest} />;
}

function SelectPositioner({ className, ...rest }: SelectPositionerProps): ReactElement {
  return <BaseSelect.Positioner className={cn(selectPositionerRecipe(), className)} {...rest} />;
}

function SelectPopup({ className, ...rest }: SelectPopupProps): ReactElement {
  return <BaseSelect.Popup className={cn(selectPopupRecipe(), className)} {...rest} />;
}

function SelectList({ className, ...rest }: SelectListProps): ReactElement {
  return <BaseSelect.List className={cn(selectListRecipe(), className)} {...rest} />;
}

function SelectItem({ className, ...rest }: SelectItemProps): ReactElement {
  return <BaseSelect.Item className={cn(selectItemRecipe(), className)} {...rest} />;
}

function SelectItemText({ className, ...rest }: SelectItemTextProps): ReactElement {
  return <BaseSelect.ItemText className={cn(selectItemTextRecipe(), className)} {...rest} />;
}

function SelectItemIndicator({ className, ...rest }: SelectItemIndicatorProps): ReactElement {
  return (
    <BaseSelect.ItemIndicator className={cn(selectItemIndicatorRecipe(), className)} {...rest} />
  );
}

function SelectArrow({ className, ...rest }: SelectArrowProps): ReactElement {
  return <BaseSelect.Arrow className={cn(selectArrowRecipe(), className)} {...rest} />;
}

function SelectScrollUpArrow({ className, ...rest }: SelectScrollUpArrowProps): ReactElement {
  return (
    <BaseSelect.ScrollUpArrow className={cn(selectScrollArrowRecipe(), className)} {...rest} />
  );
}

function SelectScrollDownArrow({ className, ...rest }: SelectScrollDownArrowProps): ReactElement {
  return (
    <BaseSelect.ScrollDownArrow className={cn(selectScrollArrowRecipe(), className)} {...rest} />
  );
}

function SelectGroup({ className, ...rest }: SelectGroupProps): ReactElement {
  return <BaseSelect.Group className={cn(selectGroupRecipe(), className)} {...rest} />;
}

function SelectGroupLabel({ className, ...rest }: SelectGroupLabelProps): ReactElement {
  return <BaseSelect.GroupLabel className={cn(selectGroupLabelRecipe(), className)} {...rest} />;
}

function SelectSeparator({ className, ...rest }: SelectSeparatorProps): ReactElement {
  return <BaseSelect.Separator className={cn(selectSeparatorRecipe(), className)} {...rest} />;
}

/**
 * The catalog's select, as ONE namespace object whose keys mirror Base UI's
 * nineteen part names 1:1 — the catalog-wide convention for multi-part
 * components, so a caller who knows the Base UI docs already knows this API.
 *
 * A minimal usable select is Root > Trigger(Value, Icon) plus a portalled
 * Positioner > Popup > List > Item(ItemText, ItemIndicator); the rest
 * (Label, Backdrop, Arrow, the scroll arrows, Group/GroupLabel/Separator) are
 * opt-in.
 */
export const Select = {
  Root: BaseSelect.Root,
  Label: SelectLabel,
  Trigger: SelectTrigger,
  Value: SelectValue,
  Icon: SelectIcon,
  Portal: BaseSelect.Portal,
  Backdrop: SelectBackdrop,
  Positioner: SelectPositioner,
  Popup: SelectPopup,
  List: SelectList,
  Item: SelectItem,
  ItemIndicator: SelectItemIndicator,
  ItemText: SelectItemText,
  Arrow: SelectArrow,
  ScrollDownArrow: SelectScrollDownArrow,
  ScrollUpArrow: SelectScrollUpArrow,
  Group: SelectGroup,
  GroupLabel: SelectGroupLabel,
  Separator: SelectSeparator,
};
