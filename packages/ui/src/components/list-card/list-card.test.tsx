import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { ListRow } from "../list-row/list-row";
import { ListCard } from "./list-card";

/*
 * SPEC (Wallow-lrlm.3.5). `ListCard` is the outer half of the list shape both
 * wallow-web list features hand-roll verbatim today —
 * `features/organizations/components/OrganizationList.tsx` and
 * `features/apps/components/AppList.tsx` each end in
 *
 *   <div className="bg-card rounded-lg shadow-sm border border-border overflow-hidden">
 *     <ul data-testid="organizations-table" className="divide-y divide-border">
 *
 * with a comment explaining why they cannot use the catalog `Card` (its fixed
 * `p-6 space-y-6` fights rows that must bleed to the card edge). That is the
 * component this file specifies: a card-shaped surface whose ONE child is the
 * divided `<ul>`, and whose test id is DERIVED from a `name` prop rather than
 * hand-passed, so the shipped `organizations-table` / `apps-table` selectors
 * keep resolving after the migration (F5) with the app naming the list once.
 *
 * Class assertions are order-free sets, per the Button/PageHeader exemplars.
 */

/** The card surface — the recipe a caller's `className` merges over. */
const SURFACE_CLASSES = [
  "bg-card",
  "rounded-lg",
  "shadow-sm",
  "border",
  "border-border",
  "overflow-hidden",
];

/** The `<ul>` inside it: hairlines between rows, nothing else. */
const LIST_CLASSES = ["divide-y", "divide-border"];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function rootOf(container: HTMLElement): HTMLElement {
  const root = container.firstElementChild;
  expect(root).not.toBeNull();
  return root as HTMLElement;
}

function listOf(container: HTMLElement): HTMLUListElement {
  const list = container.querySelector("ul");
  expect(list).not.toBeNull();
  return list as HTMLUListElement;
}

describe("ListCard", () => {
  it("renders a card surface carrying exactly the recipe", async () => {
    const { container } = await render(<ListCard name="organizations" />);

    expect(classSet(rootOf(container))).toEqual([...SURFACE_CLASSES].toSorted());
  });

  it("wraps a single divided ul as the surface's only child", async () => {
    const { container } = await render(<ListCard name="organizations" />);

    const root = rootOf(container);
    expect(root.children.length).toBe(1);

    const list = listOf(container);
    expect(list.parentElement).toBe(root);
    expect(classSet(list)).toEqual([...LIST_CLASSES].toSorted());
  });

  it("derives the ul's data-testid from the name prop", async () => {
    const { container } = await render(<ListCard name="organizations" />);

    // The shipped selector, unchanged: `organizations-table`.
    expect(listOf(container).getAttribute("data-testid")).toBe("organizations-table");
  });

  it("derives a different list's test id from its own name", async () => {
    const { container } = await render(<ListCard name="apps" />);

    expect(listOf(container).getAttribute("data-testid")).toBe("apps-table");
  });

  it("leaves the surface without a test id of its own", async () => {
    const { container } = await render(<ListCard name="organizations" />);

    // The shipped markup stamps the id on the `<ul>` only. Two elements
    // answering a `*-table` query would make the E2E selector ambiguous.
    expect(rootOf(container).hasAttribute("data-testid")).toBe(false);
  });

  it("renders its children inside the ul, not beside it", async () => {
    const { container } = await render(
      <ListCard name="organizations">
        <li data-testid="organization-item">Acme</li>
      </ListCard>,
    );

    const row = container.querySelector('[data-testid="organization-item"]');
    expect(row).not.toBeNull();
    expect(row?.parentElement).toBe(listOf(container));
  });

  it("renders every row of a list in order", async () => {
    const { container } = await render(
      <ListCard name="organizations">
        <li>Acme</li>
        <li>Globex</li>
        <li>Initech</li>
      </ListCard>,
    );

    const list = listOf(container);
    expect(list.children.length).toBe(3);
    expect([...list.children].map((child) => child.textContent)).toEqual([
      "Acme",
      "Globex",
      "Initech",
    ]);
  });

  it("lets a caller className override the surface recipe", async () => {
    const { container } = await render(<ListCard name="organizations" className="rounded-none" />);

    const root = rootOf(container);
    expect(root.classList.contains("rounded-none")).toBe(true);
    expect(root.classList.contains("rounded-lg")).toBe(false);
    // The rest of the recipe survives the override.
    expect(root.classList.contains("bg-card")).toBe(true);
    expect(root.classList.contains("overflow-hidden")).toBe(true);
  });

  it("passes rest props through to the surface", async () => {
    const { container } = await render(
      <ListCard name="organizations" id="organizations-card" aria-label="Organizations" />,
    );

    const root = rootOf(container);
    expect(root.id).toBe("organizations-card");
    expect(root.getAttribute("aria-label")).toBe("Organizations");
  });

  it("keeps the name out of the surface's attributes", async () => {
    const { container } = await render(<ListCard name="organizations" />);

    // `name` is this component's own prop, not a DOM attribute to leak.
    expect(rootOf(container).hasAttribute("name")).toBe(false);
  });

  it("reproduces the shipped organizations markup with ListRow children", async () => {
    const { container } = await render(
      <ListCard name="organizations">
        <ListRow name="organization">Acme</ListRow>
        <ListRow name="organization">Globex</ListRow>
      </ListCard>,
    );

    // The integration the F5 migration performs: `organizations-table` holding
    // `organization-item` rows, each a direct `<li>` child of the list.
    const list = listOf(container);
    expect(list.getAttribute("data-testid")).toBe("organizations-table");

    const rows = container.querySelectorAll('[data-testid="organization-item"]');
    expect(rows.length).toBe(2);
    for (const row of rows) {
      expect(row.tagName).toBe("LI");
      expect(row.parentElement).toBe(list);
    }
  });
});
