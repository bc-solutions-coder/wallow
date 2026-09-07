import { Button as BaseButton } from "@base-ui/react/button";
import {
  isValidElement,
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type ComponentProps,
  type ReactElement,
} from "react";

import { cn } from "../../core/cn";
import { buttonRecipe, type ButtonRecipeProps } from "./button.styles";

/** The visual variants the shared button offers. `primary` is the default. */
export type ButtonVariant = NonNullable<ButtonRecipeProps["variant"]>;

/**
 * Every Base UI `Button` prop (`render`, `nativeButton`, `disabled` and the
 * native button attributes) plus the recipe's `variant`, `size`, `width`,
 * `shape` and `surface`.
 *
 * `className` is deliberately narrowed back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. Every component in this catalog makes
 * the same narrowing, so a caller's `className` always means "utilities merged
 * over the recipe, last one wins".
 */
export interface ButtonProps
  extends Omit<ComponentProps<typeof BaseButton>, "className">, ButtonRecipeProps {
  readonly className?: string;
}

/**
 * `useLayoutEffect` in the browser, `useEffect` on the server. Both apps render
 * this catalog through TanStack Start's SSR pass, where React logs a warning for
 * every `useLayoutEffect` it cannot run. The browser arm matters: it lands the
 * corrected role BEFORE paint, so no user or assistive technology ever observes
 * the intermediate value.
 */
const useIsomorphicLayoutEffect: typeof useLayoutEffect =
  typeof document === "undefined" ? useEffect : useLayoutEffect;

/**
 * This is an OPTIMISTIC SEED for the state below, never the authority. It exists
 * so the server-rendered HTML already says "link" for the cases that can be
 * known without a DOM; the layout effect still has the last word, and a caller
 * whose `render` is a COMPONENT (TanStack Router's `Link`) is invisible here by
 * construction — a component's element type is a function, and only mounting it
 * reveals the anchor.
 */
function rendersAnchorWithHref(render: unknown): boolean {
  if (!isValidElement(render) || render.type !== "a") {
    return false;
  }

  const { href } = render.props as { readonly href?: unknown };
  return typeof href === "string" && href !== "";
}

/**
 * The catalog's button, built on Base UI so state arrives as `data-*`
 * attributes and `render` can compose the recipe onto another element.
 *
 * The role is supplied as a DEFAULT, not an override: it is spread BEFORE
 * `rest`, and Base UI merges the `render` element's own props last, so a caller
 * who needs `role="menuitem"` still wins from either side.
 */
export function Button({
  variant,
  size,
  width,
  shape,
  surface,
  className,
  ref,
  ...rest
}: ButtonProps): ReactElement {
  const elementRef = useRef<HTMLElement | null>(null);
  const [isLink, setIsLink] = useState<boolean>(() => rendersAnchorWithHref(rest.render));

  const composedRef = useCallback(
    (element: HTMLElement | null): void => {
      elementRef.current = element;
      if (typeof ref === "function") {
        ref(element);
        return;
      }
      if (ref !== null && ref !== undefined) {
        ref.current = element;
      }
    },
    [ref],
  );

  useIsomorphicLayoutEffect(() => {
    const element: HTMLElement | null = elementRef.current;
    // `HTMLAnchorElement.href` reads back the RESOLVED url, so it is the empty
    // string exactly when the attribute is absent. An `<a>` with no destination
    // is not a link and keeps Base UI's button role.
    setIsLink(element instanceof HTMLAnchorElement && element.href !== "");
  });

  // Every recipe group is destructured, never spread: `size` and `width` are
  // real HTML attribute names, so a forgotten one lands in the markup instead
  // of in the class list.
  return (
    <BaseButton
      ref={composedRef}
      className={cn(buttonRecipe({ variant, size, width, shape, surface }), className)}
      // Spread rather than `role={isLink ? "link" : undefined}`: Base UI merges
      // with `Object.assign` semantics, so an explicit `undefined` would delete
      // the `role="button"` that a composed `<div>` depends on.
      {...(isLink ? { role: "link" } : {})}
      {...rest}
    />
  );
}
