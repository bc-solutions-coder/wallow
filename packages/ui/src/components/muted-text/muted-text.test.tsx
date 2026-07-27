import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { MutedText } from "./muted-text";

/*
 * REFIT SPEC (Wallow-m5aq.2.13). Pins the pre-refit contract (a `<p>` carrying
 * the muted recipe, children, an additive caller className, app-owned
 * data-testid) and adds the refit requirement: a conflicting caller className
 * must WIN, which only `cn()` over a cva recipe delivers. Five wallow-web call
 * sites already pass `className="text-center py-12"`, so the additive case is a
 * shipped-behaviour pin, not a hypothetical.
 *
 * Class assertions are order-free sets, per the Button exemplar.
 */

/** The muted paragraph recipe — the strongest single recipe in the inventory. */
const MUTED_CLASSES = ["text-sm", "text-muted-foreground"];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function onlyParagraph(container: HTMLElement): HTMLParagraphElement {
  const p = container.querySelector("p");
  expect(p).not.toBeNull();
  return p as HTMLParagraphElement;
}

describe("MutedText", () => {
  it("renders a paragraph with the exact muted recipe and its children", async () => {
    const { container } = await render(<MutedText>Forgot password?</MutedText>);

    const p = onlyParagraph(container);
    expect(classSet(p)).toEqual([...MUTED_CLASSES].toSorted());
    expect(p.textContent).toBe("Forgot password?");
  });

  it("appends a caller-supplied className to the recipe", async () => {
    const { container } = await render(<MutedText className="mt-1">Tagline</MutedText>);

    expect(classSet(onlyParagraph(container))).toEqual([...MUTED_CLASSES, "mt-1"].toSorted());
  });

  it("renders the wallow-web loading-state call site", async () => {
    const { container } = await render(
      <MutedText className="text-center py-12">Loading…</MutedText>,
    );

    expect(classSet(onlyParagraph(container))).toEqual(
      [...MUTED_CLASSES, "text-center", "py-12"].toSorted(),
    );
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<MutedText data-testid="login-hint">hint</MutedText>);

    expect(container.querySelector('[data-testid="login-hint"]')).not.toBeNull();
  });

  it("lets a caller className override the muted colour", async () => {
    // The refit requirement. Pre-refit the append kept both colour utilities and
    // left the winner to stylesheet order.
    const { container } = await render(<MutedText className="text-destructive">Failed</MutedText>);

    const p = onlyParagraph(container);
    expect(p.classList.contains("text-destructive")).toBe(true);
    expect(p.classList.contains("text-muted-foreground")).toBe(false);
    expect(p.classList.contains("text-sm")).toBe(true);
  });

  it("lets a caller className override the text size", async () => {
    const { container } = await render(<MutedText className="text-lg">Big</MutedText>);

    const p = onlyParagraph(container);
    expect(p.classList.contains("text-lg")).toBe(true);
    expect(p.classList.contains("text-sm")).toBe(false);
    expect(p.classList.contains("text-muted-foreground")).toBe(true);
  });
});
