import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { CardHeader } from "./card-header";

/*
 * CardHeader is the title-and-description pair sourced from 11 hand-rolled
 * `CardHeading` functions across wallow-auth, each of which rebuilt the same
 * stack: a `space-y-1` wrapper, an h2 at the card-heading step, and an optional
 * muted paragraph beneath it.
 *
 * The load-bearing claim is that the h2 lives HERE rather than at the call site,
 * so the 20px card-heading standard is guaranteed by construction. `CardTitle`
 * already owns that step, so this file pins the COMPOSITION — the element, the
 * ordering, the optional description — and leaves the type scale to
 * `card.test.tsx` and the measured `HeadingScale` story.
 *
 * Class assertions are order-free sets, per the Card exemplar: tailwind-merge is
 * free to reorder, and set equality also fails on a stray extra utility.
 */

/** The wrapper's own recipe, minus anything a caller merges over it. */
const WRAPPER_CLASSES = ["space-y-1"];

function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function firstChild(container: HTMLElement): HTMLElement {
  const el = container.firstElementChild;
  expect(el).not.toBeNull();
  return el as HTMLElement;
}

function onlyHeading(container: HTMLElement): HTMLHeadingElement {
  const heading = container.querySelector("h2");
  expect(heading).not.toBeNull();
  return heading as HTMLHeadingElement;
}

describe("CardHeader", () => {
  it("renders the title as the surface's h2", async () => {
    const { container } = await render(<CardHeader title="Create an account" />);

    expect(onlyHeading(container).textContent).toBe("Create an account");
  });

  it("renders no paragraph when no description is given", async () => {
    // The oracle's headings are not all two-line: MfaEnroll and Register ship a
    // description, ErrorPage does not. An empty <p> would leave a rhythm gap
    // under the ones that omit it.
    const { container } = await render(<CardHeader title="Something went wrong" />);

    expect(container.querySelector("p")).toBeNull();
  });

  it("renders the description beneath the title", async () => {
    const { container } = await render(
      <CardHeader title="Create an account" description="Enter your details to get started" />,
    );

    const paragraph = container.querySelector("p");
    expect(paragraph).not.toBeNull();
    expect((paragraph as HTMLParagraphElement).textContent).toBe(
      "Enter your details to get started",
    );

    // Order is the contract: a description above its heading reads as a caption.
    const heading = onlyHeading(container);
    expect(heading.compareDocumentPosition(paragraph as Node)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
  });

  it("renders the default wrapper recipe", async () => {
    const { container } = await render(<CardHeader title="Sign in" />);

    expect(classSet(firstChild(container))).toEqual([...WRAPPER_CLASSES].toSorted());
  });

  it("merges a caller className over the wrapper, keeping the rhythm", async () => {
    // RegisterForm's heading is centred (`space-y-1 text-center`) while every
    // other screen's is not, so the alignment has to reach the wrapper without
    // displacing the rhythm utility.
    const { container } = await render(
      <CardHeader title="Create an account" className="text-center" />,
    );

    expect(classSet(firstChild(container))).toEqual([...WRAPPER_CLASSES, "text-center"].toSorted());
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<CardHeader title="Sign in" data-testid="login-heading" />);

    expect(container.querySelector('[data-testid="login-heading"]')).not.toBeNull();
  });

  it("does not put the title on the wrapper as an HTML title attribute", async () => {
    // `title` is a real HTMLAttributes member, so a passthrough that spreads it
    // would also stamp a tooltip on the wrapper. The prop is ours, not the DOM's.
    const { container } = await render(<CardHeader title="Sign in" />);

    expect(firstChild(container).getAttribute("title")).toBeNull();
  });
});
