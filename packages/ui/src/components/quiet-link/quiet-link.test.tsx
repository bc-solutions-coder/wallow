import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { QuietLink } from "./quiet-link";

/*
 * QuietLink is the muted secondary link — card footers ("Back to sign in"),
 * "Forgot password?", "Skip for now". Sourced from 13 hand-spelled anchors
 * across both apps, all of which were plain `<a>` elements, which is why this
 * takes `AnchorHTMLAttributes` rather than composing a router Link.
 *
 * It is deliberately NOT `Button variant="link"`: that arm is primary-coloured
 * and announces itself with an underline, because it stands in for an action.
 * This one recedes.
 *
 * Class assertions are order-free sets, per the Card exemplar: tailwind-merge is
 * free to reorder, and set equality also fails on a stray extra utility.
 */

/** The recipe's own classes, minus anything a caller merges over them. */
const RECIPE_CLASSES = ["text-sm", "text-muted-foreground", "hover:text-foreground"];

function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function anchor(container: HTMLElement): HTMLAnchorElement {
  const element = container.querySelector("a");
  expect(element).not.toBeNull();
  return element as HTMLAnchorElement;
}

describe("QuietLink", () => {
  it("renders an anchor carrying the given href", async () => {
    const { container } = await render(<QuietLink href="/login">Back to sign in</QuietLink>);

    expect(anchor(container).getAttribute("href")).toBe("/login");
    expect(anchor(container).textContent).toBe("Back to sign in");
  });

  it("renders the recipe by default", async () => {
    const { container } = await render(<QuietLink href="/login">Back</QuietLink>);

    expect(classSet(anchor(container))).toEqual([...RECIPE_CLASSES].toSorted());
  });

  it("merges an additive caller className over the recipe", async () => {
    // InvitationScreen's footer link is a centred block; the layout utilities
    // have to reach the anchor without displacing the colour or the scale.
    const { container } = await render(
      <QuietLink href="/login" className="block text-center">
        Back
      </QuietLink>,
    );

    expect(classSet(anchor(container))).toEqual(
      [...RECIPE_CLASSES, "block", "text-center"].toSorted(),
    );
  });

  it("lets a caller className win the axis it conflicts with", async () => {
    // The cn() contract catalog-wide: last value wins, and only on the axis the
    // caller actually named. A class-string `toContain` would pass here even if
    // the recipe's own `text-sm` had survived alongside it.
    const { container } = await render(
      <QuietLink href="/login" className="text-base">
        Back
      </QuietLink>,
    );

    expect(classSet(anchor(container))).toEqual(
      ["text-base", "text-muted-foreground", "hover:text-foreground"].toSorted(),
    );
  });

  it("passes through the anchor attributes an app owns", async () => {
    const { container } = await render(
      <QuietLink href="https://example.test" target="_blank" rel="noreferrer" data-testid="footer">
        Docs
      </QuietLink>,
    );

    const element = anchor(container);
    expect(element.getAttribute("data-testid")).toBe("footer");
    expect(element.getAttribute("target")).toBe("_blank");
    expect(element.getAttribute("rel")).toBe("noreferrer");
  });
});
