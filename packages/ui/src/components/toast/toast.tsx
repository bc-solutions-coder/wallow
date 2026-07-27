import { Toast as BaseToast } from "@base-ui/react/toast";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  toastActionRecipe,
  type ToastActionRecipeProps,
  toastArrowRecipe,
  type ToastArrowRecipeProps,
  toastCloseRecipe,
  type ToastCloseRecipeProps,
  toastContentRecipe,
  type ToastContentRecipeProps,
  toastDescriptionRecipe,
  type ToastDescriptionRecipeProps,
  toastPositionerRecipe,
  type ToastPositionerRecipeProps,
  toastRootRecipe,
  type ToastRootRecipeProps,
  toastTitleRecipe,
  type ToastTitleRecipeProps,
  toastViewportRecipe,
  type ToastViewportRecipeProps,
} from "./toast.styles";

/**
 * Four of Base UI's thirteen namespace members are re-exported UNWRAPPED,
 * because none of them can carry a recipe:
 *
 *   - `Provider` renders no HTML element at all. It owns the toast store (the
 *     default `timeout`, the `limit`, and an optional app-wide `toastManager`),
 *     which is state, not chrome.
 *   - `Portal` renders only the structural `<div data-base-ui-portal>` Base UI
 *     appends to `<body>`. Note that Toast is the one overlay in this catalog
 *     where the portal is OPT-IN: a `Toast.Viewport` with no `Toast.Portal`
 *     around it stays exactly where the caller rendered it.
 *   - `useToastManager` (the hook every consumer calls to add, update, close and
 *     await toasts) and `createToastManager` (the same API as a standalone
 *     object, for code that has to raise a toast from outside React) are not
 *     components at all.
 *
 * Both manager entry points are ALSO exported as top-level named exports below,
 * because a hook read off a namespace object reads badly at the call site
 * (`Toast.useToastManager()`), while the namespace keys still have to mirror
 * Base UI's 1:1 — the catalog-wide multi-part convention.
 */

/** Every Base UI `Toast.Provider` prop. Re-exported unwrapped, so no recipe props. */
export type ToastProviderProps = ComponentProps<typeof BaseToast.Provider>;

/** Every Base UI `Toast.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type ToastPortalProps = ComponentProps<typeof BaseToast.Portal>;

/*
 * `className` is deliberately narrowed back to `string` on every wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
 */

/** Every Base UI `Toast.Viewport` prop, with `className` narrowed to `string`. */
export interface ToastViewportProps
  extends Omit<ComponentProps<typeof BaseToast.Viewport>, "className">, ToastViewportRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Toast.Root` prop, with `className` narrowed to `string`. */
export interface ToastRootProps
  extends Omit<ComponentProps<typeof BaseToast.Root>, "className">, ToastRootRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Toast.Content` prop, with `className` narrowed to `string`. */
export interface ToastContentProps
  extends Omit<ComponentProps<typeof BaseToast.Content>, "className">, ToastContentRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Toast.Title` prop, with `className` narrowed to `string`. */
export interface ToastTitleProps
  extends Omit<ComponentProps<typeof BaseToast.Title>, "className">, ToastTitleRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Toast.Description` prop, with `className` narrowed to `string`. */
export interface ToastDescriptionProps
  extends
    Omit<ComponentProps<typeof BaseToast.Description>, "className">,
    ToastDescriptionRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Toast.Action` prop, with `className` narrowed to `string`. */
export interface ToastActionProps
  extends Omit<ComponentProps<typeof BaseToast.Action>, "className">, ToastActionRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Toast.Close` prop, with `className` narrowed to `string`. */
export interface ToastCloseProps
  extends Omit<ComponentProps<typeof BaseToast.Close>, "className">, ToastCloseRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Toast.Positioner` prop, with `className` narrowed to `string`. */
export interface ToastPositionerProps
  extends
    Omit<ComponentProps<typeof BaseToast.Positioner>, "className">,
    ToastPositionerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Toast.Arrow` prop, with `className` narrowed to `string`. */
export interface ToastArrowProps
  extends Omit<ComponentProps<typeof BaseToast.Arrow>, "className">, ToastArrowRecipeProps {
  readonly className?: string;
}

function ToastViewport({ className, ...rest }: ToastViewportProps): ReactElement {
  return <BaseToast.Viewport className={cn(toastViewportRecipe(), className)} {...rest} />;
}

function ToastRoot({ className, ...rest }: ToastRootProps): ReactElement {
  return <BaseToast.Root className={cn(toastRootRecipe(), className)} {...rest} />;
}

function ToastContent({ className, ...rest }: ToastContentProps): ReactElement {
  return <BaseToast.Content className={cn(toastContentRecipe(), className)} {...rest} />;
}

function ToastTitle({ className, ...rest }: ToastTitleProps): ReactElement {
  return <BaseToast.Title className={cn(toastTitleRecipe(), className)} {...rest} />;
}

function ToastDescription({ className, ...rest }: ToastDescriptionProps): ReactElement {
  return <BaseToast.Description className={cn(toastDescriptionRecipe(), className)} {...rest} />;
}

function ToastAction({ className, ...rest }: ToastActionProps): ReactElement {
  return <BaseToast.Action className={cn(toastActionRecipe(), className)} {...rest} />;
}

function ToastClose({ className, ...rest }: ToastCloseProps): ReactElement {
  return <BaseToast.Close className={cn(toastCloseRecipe(), className)} {...rest} />;
}

function ToastPositioner({ className, ...rest }: ToastPositionerProps): ReactElement {
  return <BaseToast.Positioner className={cn(toastPositionerRecipe(), className)} {...rest} />;
}

function ToastArrow({ className, ...rest }: ToastArrowProps): ReactElement {
  return <BaseToast.Arrow className={cn(toastArrowRecipe(), className)} {...rest} />;
}

/**
 * Adds, updates, closes and awaits toasts from inside a `Toast.Provider`. The
 * same function Base UI publishes, re-exported so a consumer imports it beside
 * the parts rather than off the namespace object.
 */
export const useToastManager = BaseToast.useToastManager;

/**
 * The manager API as a standalone object, for code that has to raise a toast
 * from outside React (an API client, a router hook). Hand the result to
 * `Toast.Provider`'s `toastManager` prop and its `add`/`close`/`update`/`promise`
 * drive the same viewport.
 */
export const createToastManager = BaseToast.createToastManager;

export type {
  ToastManager,
  ToastManagerAddOptions,
  ToastManagerPromiseOptions,
  ToastManagerUpdateOptions,
  ToastObject,
  UseToastManagerReturnValue,
} from "@base-ui/react/toast";

/**
 * The shape of the `Toast` namespace object, spelled out as one `typeof` per key.
 *
 * This annotation is load-bearing, not decoration: Base UI 1.6.0's
 * `ToastPortal.d.ts` inlines the `FloatingPortalLite.Props` generic into
 * `ToastPortal`'s own signature instead of routing it through the exported
 * `ToastPortalProps` alias the way `TooltipPortal.d.ts` does. Left to infer the
 * namespace object's type, TypeScript has to print that inlined type and fails
 * with TS2742 ("cannot be named without a reference to
 * `@base-ui/react/utils/FloatingPortalLite.mjs`"). With an explicit annotation it
 * only checks assignability, and every member here is namable through the
 * `BaseToast` import.
 */
export interface ToastNamespace {
  readonly Provider: typeof BaseToast.Provider;
  readonly Portal: typeof BaseToast.Portal;
  readonly Viewport: typeof ToastViewport;
  readonly Root: typeof ToastRoot;
  readonly Content: typeof ToastContent;
  readonly Title: typeof ToastTitle;
  readonly Description: typeof ToastDescription;
  readonly Action: typeof ToastAction;
  readonly Close: typeof ToastClose;
  readonly Positioner: typeof ToastPositioner;
  readonly Arrow: typeof ToastArrow;
  readonly useToastManager: typeof BaseToast.useToastManager;
  readonly createToastManager: typeof BaseToast.createToastManager;
}

/**
 * The catalog's toast, as ONE namespace object whose keys mirror Base UI's
 * thirteen namespace members 1:1 — the catalog-wide convention for multi-part
 * components, so a caller who knows the Base UI docs already knows this API.
 *
 * A minimal usable toast is a `Provider` wrapping the app, one `Viewport`
 * mounted once at the root, and a `Root(Content(Title, Description), Close)`
 * rendered per entry of `useToastManager().toasts`. `Portal` moves that viewport
 * to `<body>`; `Positioner` and `Arrow` are the opt-in anchored form, where a
 * toast points at an element instead of stacking in the corner.
 */
export const Toast: ToastNamespace = {
  Provider: BaseToast.Provider,
  Portal: BaseToast.Portal,
  Viewport: ToastViewport,
  Root: ToastRoot,
  Content: ToastContent,
  Title: ToastTitle,
  Description: ToastDescription,
  Action: ToastAction,
  Close: ToastClose,
  Positioner: ToastPositioner,
  Arrow: ToastArrow,
  useToastManager: BaseToast.useToastManager,
  createToastManager: BaseToast.createToastManager,
};
