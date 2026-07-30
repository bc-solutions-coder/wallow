import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { cardRecipe } from "../card/card.styles";
import { textRecipe } from "../text/text.styles";
import { EmptyState } from "./empty-state";

/*
 * SPEC (Wallow-lrlm.3.3). EmptyState is the "nothing here yet" card both
 * wallow-web list components hand-roll today —
 * `features/organizations/components/OrganizationList.tsx`
 * (`OrganizationsEmptyState`: a card-shaped div holding a 🏢 emoji, an `<h2>`
 * message and a `<p>` of supporting copy) and `features/apps/components/
 * AppList.tsx` (`AppsEmptyState`: the same block with a 🐷). Neither ships an
 * action today; the optional `action` slot is what lets the migrated call sites
 * grow a "create your first one" button without forking the component.
 *
 * Two compositions are load-bearing and are asserted the only way a render can:
 *
 *   - the SURFACE is the real `Card`, so the root's classes are exactly
 *     `cardRecipe()` plus the spacing block — not a hand-rolled
 *     `bg-card rounded-lg border` string that happens to look the same;
 *   - the message and the description render THROUGH `Text`, so their class
 *     sets are exactly what `textRecipe` produces for the scale EmptyState asks
 *     for. That equality is what makes the call sites' `text-foreground/60`
 *     unreachable: `color="muted"` is a token, and Text carries no opacity
 *     suffix in any combination.
 *
 * `as="h2"` alone would derive the `title` scale (`text-3xl`), so
 * `variant="subheading"` is load-bearing — it is what preserves the shipped
 * `text-xl font-semibold` message. The source-level half of both composition
 * claims (which modules the component imports) lives in
 * `empty-state.composition.test.ts`, on the node project.
 *
 * Class assertions are order-free sets, per the Button/MutedText exemplars.
 */

/** The spacing block EmptyState hands to `Card`, replacing its `p-6 space-y-6`. */
const SPACING_CLASSES = ["p-12", "flex", "flex-col", "items-center", "gap-2", "text-center"];

/** The icon slot above the message. */
const ICON_CLASSES = ["text-7xl", "leading-none", "mb-2"];

/** The action slot under the copy. */
const ACTION_CLASSES = ["mt-4", "flex", "items-center", "justify-center", "gap-3"];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

/** A recipe's class string as the same order-free set. */
function recipeSet(classes: string): string[] {
  return classes.trim().split(/\s+/u).toSorted();
}

function rootOf(container: HTMLElement): HTMLElement {
  const root = container.firstElementChild;
  expect(root).not.toBeNull();
  return root as HTMLElement;
}

function messageOf(container: HTMLElement): HTMLHeadingElement {
  const message = container.querySelector("h2");
  expect(message).not.toBeNull();
  return message as HTMLHeadingElement;
}

function descriptionOf(container: HTMLElement): HTMLParagraphElement {
  const description = container.querySelector("p");
  expect(description).not.toBeNull();
  return description as HTMLParagraphElement;
}

describe("EmptyState", () => {
  it("renders the message as an h2 through Text's subheading scale", async () => {
    const { container } = await render(<EmptyState message="No organizations yet." />);

    const message = messageOf(container);
    expect(message.textContent).toBe("No organizations yet.");
    // Exactly the recipe, i.e. the `subheading` scale in the default foreground
    // colour — NOT the `title` scale `as="h2"` would derive on its own.
    expect(classSet(message)).toEqual(recipeSet(textRecipe({ variant: "subheading" })));
  });

  it("carries the Card surface plus the empty-state spacing block on its root", async () => {
    const { container } = await render(<EmptyState message="No apps yet." />);

    // Card's own recipe, unrestated: EmptyState composes the component rather
    // than copying its three utilities into a local class string.
    expect(classSet(rootOf(container))).toEqual(
      [...recipeSet(cardRecipe()), ...SPACING_CLASSES].toSorted(),
    );
  });

  it("renders only the message when nothing else is supplied", async () => {
    const { container } = await render(<EmptyState message="No apps yet." />);

    // No empty icon div, no empty paragraph, no empty action row: an optional
    // slot that is not supplied leaves no element behind to collect the gap.
    expect(rootOf(container).children.length).toBe(1);
    expect(container.querySelector("p")).toBeNull();
  });

  it("renders the icon in a slot above the message", async () => {
    const { container } = await render(
      <EmptyState icon={<span data-testid="org-emoji">🏢</span>} message="No organizations yet." />,
    );

    const slot = rootOf(container).firstElementChild;
    expect(slot).not.toBeNull();
    expect(slot).not.toBe(messageOf(container));
    expect(classSet(slot as Element)).toEqual([...ICON_CLASSES].toSorted());
    expect(slot?.querySelector('[data-testid="org-emoji"]')).not.toBeNull();
  });

  it("hides the icon slot from assistive technology", async () => {
    const { container } = await render(<EmptyState icon="🏢" message="No organizations yet." />);

    // The emoji repeats what the message already says. Leaving it exposed makes
    // a screen reader announce "house building, No organizations yet."
    expect(rootOf(container).firstElementChild?.getAttribute("aria-hidden")).toBe("true");
  });

  it("renders the description through Text's muted body scale", async () => {
    const { container } = await render(
      <EmptyState
        message="No organizations yet."
        description="Nothing belongs here yet. Get started by creating your first organization."
      />,
    );

    const description = descriptionOf(container);
    expect(description.textContent).toBe(
      "Nothing belongs here yet. Get started by creating your first organization.",
    );
    // The `text-foreground/60` erasure: one semantic token, no opacity suffix.
    expect(classSet(description)).toEqual(
      recipeSet(textRecipe({ variant: "body", color: "muted" })),
    );
    expect(classSet(description).filter((name) => name.includes("/"))).toEqual([]);
  });

  it("places the description under the message", async () => {
    const { container } = await render(
      <EmptyState message="No apps yet." description="Nothing has been registered." />,
    );

    const message = messageOf(container);
    const description = descriptionOf(container);
    expect(description.parentElement).toBe(message.parentElement);
    // DOCUMENT_POSITION_FOLLOWING: the description comes after the message.
    expect(message.compareDocumentPosition(description) & Node.DOCUMENT_POSITION_FOLLOWING).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
  });

  it("renders the action in a slot at the bottom of the card", async () => {
    const { container } = await render(
      <EmptyState
        message="No apps yet."
        action={
          <button data-testid="apps-create-first" type="button">
            Register your first app
          </button>
        }
      />,
    );

    const slot = rootOf(container).lastElementChild;
    expect(slot).not.toBeNull();
    expect(slot).not.toBe(messageOf(container));
    expect(classSet(slot as Element)).toEqual([...ACTION_CLASSES].toSorted());
    expect(slot?.querySelector('[data-testid="apps-create-first"]')).not.toBeNull();
  });

  it("omits the action slot when no action is given", async () => {
    const { container } = await render(
      <EmptyState
        icon="🐷"
        message="No apps yet."
        description="Nothing has been registered here."
      />,
    );

    // The shipped wallow-web shape: icon, message, description and no CTA.
    expect(rootOf(container).children.length).toBe(3);
  });

  it("renders all four slots in order", async () => {
    const { container } = await render(
      <EmptyState
        icon="🏢"
        message="No organizations yet."
        description="Nothing belongs here yet."
        action={<button type="button">Create organization</button>}
      />,
    );

    const children = [...rootOf(container).children];
    expect(children.length).toBe(4);
    expect(children[1]).toBe(messageOf(container));
    expect(children[2]).toBe(descriptionOf(container));
    expect(children[3]?.textContent).toBe("Create organization");
  });

  it("lets a caller className override the recipe's padding", async () => {
    const { container } = await render(<EmptyState message="No apps yet." className="p-6" />);

    const root = rootOf(container);
    expect(root.classList.contains("p-6")).toBe(true);
    expect(root.classList.contains("p-12")).toBe(false);
    // The surface survives the override — only the conflicting utility loses.
    expect(root.classList.contains("bg-card")).toBe(true);
    expect(root.classList.contains("text-center")).toBe(true);
  });

  it("passes an app-owned data-testid and rest props through to the root", async () => {
    const { container } = await render(
      <EmptyState
        data-testid="organizations-empty-state"
        id="organizations-empty"
        message="No organizations yet."
      />,
    );

    const root = rootOf(container);
    expect(root.getAttribute("data-testid")).toBe("organizations-empty-state");
    expect(root.id).toBe("organizations-empty");
  });

  it("derives the inner test ids from the root's data-testid", async () => {
    const { container } = await render(
      <EmptyState
        data-testid="organizations-empty-state"
        icon="🏢"
        message="No organizations yet."
        description="Nothing belongs here yet."
        action={<button type="button">Create organization</button>}
      />,
    );

    // Derived, never hand-passed — the same rule packages/forms applies to a
    // field catalog, so an app names the block once and its E2E selectors follow.
    const root = rootOf(container);
    expect(root.firstElementChild?.getAttribute("data-testid")).toBe(
      "organizations-empty-state-icon",
    );
    expect(messageOf(container).getAttribute("data-testid")).toBe(
      "organizations-empty-state-message",
    );
    expect(descriptionOf(container).getAttribute("data-testid")).toBe(
      "organizations-empty-state-description",
    );
    expect(root.lastElementChild?.getAttribute("data-testid")).toBe(
      "organizations-empty-state-action",
    );
  });

  it("stamps no inner test ids when the root carries none", async () => {
    const { container } = await render(
      <EmptyState
        icon="🏢"
        message="No organizations yet."
        description="Nothing belongs here yet."
        action={<button type="button">Create organization</button>}
      />,
    );

    const root = rootOf(container);
    expect(root.firstElementChild?.hasAttribute("data-testid")).toBe(false);
    expect(messageOf(container).hasAttribute("data-testid")).toBe(false);
    expect(descriptionOf(container).hasAttribute("data-testid")).toBe(false);
    expect(root.lastElementChild?.hasAttribute("data-testid")).toBe(false);
  });

  it("renders a ReactNode message, not just a string", async () => {
    const { container } = await render(
      <EmptyState
        message={
          <>
            No organizations <span data-testid="empty-qualifier">yet</span>
          </>
        }
      />,
    );

    const message = messageOf(container);
    expect(message.querySelector('[data-testid="empty-qualifier"]')).not.toBeNull();
    // Still one heading carrying the whole message, not a nested second element.
    expect(container.querySelectorAll("h2").length).toBe(1);
  });
});
