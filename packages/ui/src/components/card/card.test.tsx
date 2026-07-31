import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { Card, CardTitle } from "./card";

/*
 * REFIT SPEC (Wallow-m5aq.2.13). Card is not Base UI-backed — it is a plain
 * styled `<div>` that predates the rebuild — so this file has two jobs:
 *
 *   1. REGRESSION PINS. Card ships in wallow-web today (`<Card className="mt-8">`,
 *      `<Card spacing="p-8 space-y-6" className="shadow-sm">`), so every prop the
 *      pre-refit component honoured is pinned here: the default recipe, the
 *      `spacing` slot with its two measured outliers, children, data-testid.
 *   2. THE REFIT REQUIREMENT. The recipe must move into card.styles.ts and reach
 *      the element through `cn()`, which is only observable from outside as
 *      OVERRIDE BEHAVIOUR: a caller `className` conflicting with a recipe utility
 *      has to WIN. The pre-refit string append kept both classes, so those tests
 *      fail until `cn()` is wired.
 *
 * Class assertions are order-free sets (`classSet`), per the Button exemplar:
 * tailwind-merge is free to reorder, so the pre-refit exact-string `toBe`
 * assertions are restated as set equality — strictly stronger than per-class
 * `contains` checks, because a stray extra utility fails too.
 */

/** The card surface, minus the `spacing` block the prop owns. */
const SURFACE_CLASSES = ["rounded-lg", "border", "border-border", "bg-card"];

/** The default `spacing` value, unchanged by the refit. */
const DEFAULT_SPACING = ["p-6", "space-y-6"];

/**
 * The heading recipe. `text-xl` is the catalog-wide heading standard adopted in
 * Wallow-io5f — the same step `Text`'s `subheading` variant carries, so the two
 * spellings of a card heading agree. It used to be `text-lg`.
 *
 * That the class is PRESENT is all this file can say: it runs in the `browser`
 * project, which loads no Tailwind, so nothing here proves the element computes
 * 20px. The measurement lives in `card.stories.tsx`'s `HeadingScale`.
 */
const TITLE_CLASSES = ["text-xl", "font-semibold", "text-card-foreground"];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
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

describe("Card", () => {
  it("renders the dominant recipe (p-6 space-y-6) by default", async () => {
    const { container } = await render(
      <Card>
        <span data-testid="child" />
      </Card>,
    );

    const card = firstChild(container);
    expect(classSet(card)).toEqual([...SURFACE_CLASSES, ...DEFAULT_SPACING].toSorted());
    expect(card.querySelector('[data-testid="child"]')).not.toBeNull();
  });

  it("covers the LoginScreen outlier via the spacing override", async () => {
    const { container } = await render(<Card spacing="p-6 space-y-4" />);

    expect(classSet(firstChild(container))).toEqual(
      [...SURFACE_CLASSES, "p-6", "space-y-4"].toSorted(),
    );
  });

  it("covers the RegisterForm bare-padding outlier via the spacing override", async () => {
    const { container } = await render(<Card spacing="p-6" />);

    expect(classSet(firstChild(container))).toEqual([...SURFACE_CLASSES, "p-6"].toSorted());
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Card data-testid="login-card" />);

    expect(container.querySelector('[data-testid="login-card"]')).not.toBeNull();
  });

  it("renders the wallow-web call site (spacing plus a non-conflicting className)", async () => {
    // InquiryDetail and CreateInquiryForm ship exactly this combination, so the
    // refit has to keep the two slots additive to each other.
    const { container } = await render(<Card spacing="p-8 space-y-6" className="shadow-sm" />);

    expect(classSet(firstChild(container))).toEqual(
      [...SURFACE_CLASSES, "p-8", "space-y-6", "shadow-sm"].toSorted(),
    );
  });

  it("lets a caller className override a recipe utility", async () => {
    // The refit requirement: `cn()` resolves the conflict and the last value
    // wins. The pre-refit string append kept `rounded-lg` next to `rounded-none`.
    const { container } = await render(<Card className="rounded-none" />);

    const card = firstChild(container);
    expect(card.classList.contains("rounded-none")).toBe(true);
    expect(card.classList.contains("rounded-lg")).toBe(false);
  });

  it("lets a caller className override the spacing slot", async () => {
    const { container } = await render(<Card className="p-8" />);

    const card = firstChild(container);
    expect(card.classList.contains("p-8")).toBe(true);
    expect(card.classList.contains("p-6")).toBe(false);
    expect(card.classList.contains("space-y-6")).toBe(true);
  });

  it("keeps recipe utilities the caller never mentions", async () => {
    const { container } = await render(<Card className="mt-8" />);

    expect(classSet(firstChild(container))).toEqual(
      [...SURFACE_CLASSES, ...DEFAULT_SPACING, "mt-8"].toSorted(),
    );
  });
});

describe("CardTitle", () => {
  it("renders an h2 with the exact title recipe and its children", async () => {
    const { container } = await render(<CardTitle>Sign in</CardTitle>);

    const heading = onlyHeading(container);
    expect(classSet(heading)).toEqual([...TITLE_CLASSES].toSorted());
    expect(heading.textContent).toBe("Sign in");
  });

  it("lets a caller className override the heading size", async () => {
    // The step the recipe itself carries, read off the recipe rather than
    // written as a literal: pinned to `text-lg` this assertion went quietly
    // vacuous the moment the standard moved to `text-xl`, since a class the
    // recipe no longer emits is absent whether or not the override works.
    const recipeSize: string | undefined = TITLE_CLASSES.find((utility) =>
      /^text-(?:xs|sm|base|lg|\d*xl)$/u.test(utility),
    );
    expect(recipeSize, "the heading recipe must carry a type step").toBeDefined();

    const { container } = await render(<CardTitle className="text-3xl">Sign in</CardTitle>);

    const heading = onlyHeading(container);
    expect(heading.classList.contains("text-3xl")).toBe(true);
    expect(heading.classList.contains(recipeSize as string)).toBe(false);
    expect(heading.classList.contains("font-semibold")).toBe(true);
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(
      <CardTitle data-testid="login-card-title">Sign in</CardTitle>,
    );

    expect(container.querySelector('[data-testid="login-card-title"]')).not.toBeNull();
  });
});
