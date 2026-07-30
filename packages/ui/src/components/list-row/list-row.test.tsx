import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it, vi } from "vitest";

import { ListRow } from "./list-row";

/*
 * SPEC (Wallow-lrlm.3.5). `ListRow` is the inner half of the list shape both
 * wallow-web list features hand-roll verbatim today (`OrganizationRow` in
 * `features/organizations/components/OrganizationList.tsx`, `AppRow` in
 * `features/apps/components/AppList.tsx`):
 *
 *   <li data-testid="organization-item"
 *       className="flex items-center justify-between px-6 py-4 hover:bg-background/50">
 *
 * Two deliberate departures from that literal string, both pinned below:
 *
 *   1. `hover:bg-background/50` becomes `hover:bg-muted`. An opacity suffix on
 *      a colour is exactly what this epic erases (see Text's recipe note) —
 *      `muted` is the semantic token that already means "subtle surface", and
 *      unlike a half-transparent page background it is correct in dark mode.
 *   2. The row gains the catalog's focus indicator (`outline-none` +
 *      `focus-visible:ring-2 focus-visible:ring-ring`, the form Button and
 *      Toolbar use). A row is about to become focusable: F4.T1 composes a
 *      TanStack Router `Link` onto it, and a keyboard user must be able to see
 *      which row they are on.
 *
 * The `render` prop is the load-bearing capability. `ListRow` wraps no headless
 * Base UI part, so it gets the contract from `@base-ui/react`'s own `useRender`
 * hook rather than inventing a second spelling: `render` accepts either a
 * ReactElement or a function, the substituted element receives the recipe, the
 * derived test id and the rest props, and the default element stays an `<li>`.
 * That is what lets F4.T1 write `render={<Link to="…" />}` and get a row that
 * navigates without `ListCard` learning anything about routing.
 *
 * Class assertions are order-free sets, per the Button/PageHeader exemplars.
 */

/** The row cell — the recipe a caller's `className` merges over. */
const ROW_CLASSES = [
  "flex",
  "items-center",
  "justify-between",
  "px-6",
  "py-4",
  "outline-none",
  "motion-safe:transition-colors",
  "hover:bg-muted",
  "focus-visible:ring-2",
  "focus-visible:ring-ring",
];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function rootOf(container: HTMLElement): HTMLElement {
  const root = container.firstElementChild;
  expect(root).not.toBeNull();
  return root as HTMLElement;
}

describe("ListRow", () => {
  it("renders an li carrying exactly the row recipe", async () => {
    const { container } = await render(<ListRow name="organization">Acme</ListRow>);

    const root = rootOf(container);
    expect(root.tagName).toBe("LI");
    expect(classSet(root)).toEqual([...ROW_CLASSES].toSorted());
  });

  it("renders its children", async () => {
    const { container } = await render(
      <ListRow name="organization">
        <span data-testid="organization-item-name">Acme</span>
        <span data-testid="organization-item-members">12</span>
      </ListRow>,
    );

    const root = rootOf(container);
    expect(root.querySelector('[data-testid="organization-item-name"]')?.textContent).toBe("Acme");
    expect(root.querySelector('[data-testid="organization-item-members"]')?.textContent).toBe("12");
  });

  it("derives the row's data-testid from the name prop", async () => {
    const { container } = await render(<ListRow name="organization">Acme</ListRow>);

    // The shipped selector, unchanged: `organization-item`.
    expect(rootOf(container).getAttribute("data-testid")).toBe("organization-item");
  });

  it("derives another list's row test id from its own name", async () => {
    const { container } = await render(<ListRow name="app">Wallow Web</ListRow>);

    expect(rootOf(container).getAttribute("data-testid")).toBe("app-item");
  });

  it("keeps the name out of the row's attributes", async () => {
    const { container } = await render(<ListRow name="organization">Acme</ListRow>);

    // `name` is this component's own prop, not a DOM attribute to leak.
    expect(rootOf(container).hasAttribute("name")).toBe(false);
  });

  it("lets a caller className override the recipe's padding", async () => {
    const { container } = await render(
      <ListRow name="organization" className="py-2">
        Acme
      </ListRow>,
    );

    const root = rootOf(container);
    expect(root.classList.contains("py-2")).toBe(true);
    expect(root.classList.contains("py-4")).toBe(false);
    // The rest of the recipe survives the override.
    expect(root.classList.contains("px-6")).toBe(true);
    expect(root.classList.contains("justify-between")).toBe(true);
  });

  it("passes rest props through to the row", async () => {
    const { container } = await render(
      <ListRow name="organization" id="organization-1" aria-label="Acme">
        Acme
      </ListRow>,
    );

    const root = rootOf(container);
    expect(root.id).toBe("organization-1");
    expect(root.getAttribute("aria-label")).toBe("Acme");
  });

  it("calls a caller's onClick when the row is clicked", async () => {
    const onClick = vi.fn();
    const { container } = await render(
      <ListRow name="organization" onClick={onClick}>
        Acme
      </ListRow>,
    );

    // The DOM's own click: the browser project loads no Tailwind, so a
    // recipe-only box can measure 0x0 and Playwright's actionability check
    // would hang (packages/ui/CLAUDE.md).
    rootOf(container).click();

    expect(onClick).toHaveBeenCalledTimes(1);
  });
});

describe("ListRow composed through render", () => {
  it("substitutes the element given as a ReactElement", async () => {
    const { container } = await render(
      <ListRow name="organization" render={<a href="/dashboard/organizations/1" />}>
        Acme
      </ListRow>,
    );

    const root = rootOf(container);
    // The row IS the link — not an `<li>` with a link inside it, which is what
    // makes the whole row a navigation target for F4.T1.
    expect(root.tagName).toBe("A");
    expect(root.getAttribute("href")).toBe("/dashboard/organizations/1");
    expect(container.querySelector("li")).toBeNull();
  });

  it("carries the row recipe onto the substituted element", async () => {
    const { container } = await render(
      <ListRow name="organization" render={<a href="/dashboard/organizations/1" />}>
        Acme
      </ListRow>,
    );

    const classes = classSet(rootOf(container));
    for (const utility of ROW_CLASSES) {
      expect(classes, utility).toContain(utility);
    }
  });

  it("carries the derived test id onto the substituted element", async () => {
    const { container } = await render(
      <ListRow name="organization" render={<a href="/dashboard/organizations/1" />}>
        Acme
      </ListRow>,
    );

    // The E2E selector keeps resolving once the row becomes a link, and now
    // clicking it navigates — that is the whole point of the composition.
    expect(rootOf(container).getAttribute("data-testid")).toBe("organization-item");
  });

  it("renders its children inside the substituted element", async () => {
    const { container } = await render(
      <ListRow name="organization" render={<a href="/dashboard/organizations/1" />}>
        <span data-testid="organization-item-name">Acme</span>
      </ListRow>,
    );

    const root = rootOf(container);
    expect(root.querySelector('[data-testid="organization-item-name"]')?.textContent).toBe("Acme");
  });

  it("keeps the substituted element's own className alongside the recipe", async () => {
    const { container } = await render(
      <ListRow name="organization" className="py-2" render={<a className="font-mono" href="/x" />}>
        Acme
      </ListRow>,
    );

    const root = rootOf(container);
    expect(root.classList.contains("font-mono")).toBe(true);
    expect(root.classList.contains("py-2")).toBe(true);
    expect(root.classList.contains("flex")).toBe(true);
  });

  it("passes rest props onto the substituted element", async () => {
    const { container } = await render(
      <ListRow name="organization" aria-label="Acme" render={<a href="/x" />}>
        Acme
      </ListRow>,
    );

    expect(rootOf(container).getAttribute("aria-label")).toBe("Acme");
  });

  it("accepts a render function as well as an element", async () => {
    const { container } = await render(
      <ListRow name="organization" render={(props) => <a {...props} href="/y" />}>
        Acme
      </ListRow>,
    );

    // The second spelling Base UI's `useRender` supports. A router `Link` that
    // needs the incoming props reshaped uses this form.
    const root = rootOf(container);
    expect(root.tagName).toBe("A");
    expect(root.getAttribute("href")).toBe("/y");
    expect(root.getAttribute("data-testid")).toBe("organization-item");
    expect(root.classList.contains("flex")).toBe(true);
  });

  it("still calls a caller's onClick on the substituted element", async () => {
    const onClick = vi.fn();
    const { container } = await render(
      <ListRow name="organization" onClick={onClick} render={<a href="#detail" />}>
        Acme
      </ListRow>,
    );

    rootOf(container).click();

    // Event handlers merge rather than replace: the substituted element's own
    // behaviour (navigation) and the caller's handler both survive.
    expect(onClick).toHaveBeenCalledTimes(1);
  });
});
