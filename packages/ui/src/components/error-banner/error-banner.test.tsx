import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { ErrorBanner } from "./error-banner";

/*
 * REFIT SPEC (Wallow-m5aq.2.13). Same two jobs as card.test.tsx: pin the
 * pre-refit contract (wrapper around a destructive-text paragraph, children,
 * app-owned data-testid on the WRAPPER) and require the refit's override
 * behaviour, which only `cn()` over a cva recipe can satisfy.
 *
 * The banner is two styled parts behind one component, so the split matters as
 * much as the classes: a caller can style the wrapper and must NOT be able to
 * reach the inner paragraph. That is asserted below in both directions.
 *
 * Class assertions are order-free sets, per the Button exemplar — tailwind-merge
 * may reorder, so the pre-refit exact-string `toBe` checks are restated as set
 * equality.
 */

/** The banner surface — the outer `<div>`. */
const WRAPPER_CLASSES = ["rounded-md", "border", "border-destructive", "bg-destructive/10", "p-3"];

/** The message text — the inner `<p>`, unreachable from outside. */
const TEXT_CLASSES = ["text-sm", "text-destructive"];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function wrapperOf(container: HTMLElement): HTMLElement {
  const wrapper = container.firstElementChild;
  expect(wrapper).not.toBeNull();
  return wrapper as HTMLElement;
}

function paragraphOf(container: HTMLElement): HTMLParagraphElement {
  const paragraph = wrapperOf(container).querySelector("p");
  expect(paragraph).not.toBeNull();
  return paragraph as HTMLParagraphElement;
}

describe("ErrorBanner", () => {
  it("renders the wrapper recipe around a destructive-text paragraph", async () => {
    const { container } = await render(<ErrorBanner>Invalid credentials</ErrorBanner>);

    expect(classSet(wrapperOf(container))).toEqual([...WRAPPER_CLASSES].toSorted());

    const paragraph = paragraphOf(container);
    expect(classSet(paragraph)).toEqual([...TEXT_CLASSES].toSorted());
    expect(paragraph.textContent).toBe("Invalid credentials");
  });

  it("passes through an app-owned data-testid onto the wrapper", async () => {
    const { container } = await render(<ErrorBanner data-testid="login-error">boom</ErrorBanner>);

    const tagged = container.querySelector('[data-testid="login-error"]');
    expect(tagged).not.toBeNull();
    expect((tagged as HTMLElement).classList.contains("border-destructive")).toBe(true);
  });

  it("lets a caller className override a wrapper recipe utility", async () => {
    // The refit requirement. Pre-refit the append kept `p-3` next to `p-6`.
    const { container } = await render(<ErrorBanner className="p-6">boom</ErrorBanner>);

    const wrapper = wrapperOf(container);
    expect(wrapper.classList.contains("p-6")).toBe(true);
    expect(wrapper.classList.contains("p-3")).toBe(false);
  });

  it("keeps wrapper utilities the caller never mentions", async () => {
    const { container } = await render(<ErrorBanner className="mt-4">boom</ErrorBanner>);

    expect(classSet(wrapperOf(container))).toEqual([...WRAPPER_CLASSES, "mt-4"].toSorted());
  });

  it("keeps a caller className off the inner paragraph", async () => {
    // Two recipes, not one: the text part stays sealed whatever the caller
    // passes, so `text-sm text-destructive` survives a conflicting override.
    const { container } = await render(<ErrorBanner className="text-lg">boom</ErrorBanner>);

    expect(classSet(paragraphOf(container))).toEqual([...TEXT_CLASSES].toSorted());
  });
});
