import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { textRecipe } from "../text/text.styles";
import { PageHeader } from "./page-header";

/*
 * SPEC (Wallow-lrlm.3.2). PageHeader is the page-level heading block both
 * wallow-web list routes hand-roll today — `dashboard/apps/index.tsx`
 * (`AppsHeader`: an `<h1 class="text-3xl font-bold text-foreground">` beside a
 * trailing CTA) and `dashboard/organizations/index.tsx`
 * (`OrganizationsHeader`: the same row with no CTA).
 *
 * The title and the description are rendered THROUGH `Text`, not as raw
 * `<h1>`/`<p>` tags, so the type scale and the semantic colour stay one
 * decision made in one place. That is asserted here the only way a render can:
 * the heading's class set must be exactly what `textRecipe` produces for the
 * scale PageHeader asks for. `as="h1"` alone would derive the `display` scale
 * (`text-4xl`), so `variant="title"` is load-bearing — it is what preserves the
 * shipped `text-3xl font-bold` heading. The source-level half of the
 * composition claim (which module the component imports) lives in
 * `page-header.composition.test.ts`, on the node project.
 *
 * Class assertions are order-free sets, per the Button/MutedText exemplars.
 */

/** The header row — the recipe the caller's `className` merges over. */
const ROOT_CLASSES = ["flex", "items-start", "justify-between", "gap-4", "mb-8"];

/** The leading column holding the title and, when given, the description. */
const TITLE_GROUP_CLASSES = ["flex", "flex-col", "gap-1"];

/** The trailing actions slot, which must not shrink under a long title. */
const ACTIONS_CLASSES = ["flex", "items-center", "gap-3", "shrink-0"];

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

function headingOf(container: HTMLElement): HTMLHeadingElement {
  const heading = container.querySelector("h1");
  expect(heading).not.toBeNull();
  return heading as HTMLHeadingElement;
}

function descriptionOf(container: HTMLElement): HTMLParagraphElement {
  const description = container.querySelector("p");
  expect(description).not.toBeNull();
  return description as HTMLParagraphElement;
}

function titleGroupOf(container: HTMLElement): HTMLElement {
  const group = headingOf(container).parentElement;
  expect(group).not.toBeNull();
  return group as HTMLElement;
}

describe("PageHeader", () => {
  it("renders the title as an h1 through Text's title scale", async () => {
    const { container } = await render(<PageHeader title="My Apps" />);

    const heading = headingOf(container);
    expect(heading.textContent).toBe("My Apps");
    // Exactly the recipe, i.e. the `title` scale in the default foreground
    // colour — NOT the `display` scale `as="h1"` would derive on its own, and
    // not a hand-rolled class string that happens to look similar.
    expect(classSet(heading)).toEqual(recipeSet(textRecipe({ variant: "title" })));
  });

  it("carries the page-header recipe on its root", async () => {
    const { container } = await render(<PageHeader title="My Apps" />);

    expect(classSet(rootOf(container))).toEqual([...ROOT_CLASSES].toSorted());
  });

  it("lays the title out in the header's leading column", async () => {
    const { container } = await render(<PageHeader title="My Apps" />);

    const root = rootOf(container);
    const group = titleGroupOf(container);
    expect(classSet(group)).toEqual([...TITLE_GROUP_CLASSES].toSorted());
    expect(root.firstElementChild).toBe(group);
  });

  it("renders only the title group when given only a title", async () => {
    const { container } = await render(<PageHeader title="My Apps" />);

    // No empty description paragraph, no empty actions slot: an optional part
    // that is not supplied leaves no element behind to collect gap spacing.
    expect(container.querySelector("p")).toBeNull();
    expect(rootOf(container).children.length).toBe(1);
  });

  it("renders the description through Text's muted small-body scale", async () => {
    const { container } = await render(
      <PageHeader title="My Apps" description="Everything you have registered." />,
    );

    const description = descriptionOf(container);
    expect(description.textContent).toBe("Everything you have registered.");
    expect(classSet(description)).toEqual(
      recipeSet(textRecipe({ variant: "bodySm", color: "muted" })),
    );
  });

  it("places the description under the title inside the same column", async () => {
    const { container } = await render(
      <PageHeader title="My Apps" description="Everything you have registered." />,
    );

    const heading = headingOf(container);
    const description = descriptionOf(container);
    expect(description.parentElement).toBe(heading.parentElement);
    // DOCUMENT_POSITION_FOLLOWING: the description comes after the heading.
    expect(heading.compareDocumentPosition(description) & Node.DOCUMENT_POSITION_FOLLOWING).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
  });

  it("renders actions in a slot at the header's trailing edge", async () => {
    const { container } = await render(
      <PageHeader
        title="My Apps"
        actions={
          <a data-testid="apps-register-link" href="/dashboard/apps/register">
            Register New App
          </a>
        }
      />,
    );

    const root = rootOf(container);
    const slot = root.lastElementChild;
    expect(slot).not.toBeNull();
    // Trailing edge, and NOT the title group: the actions are the row's last
    // child, which is what `justify-between` pushes to the far side.
    expect(slot).not.toBe(titleGroupOf(container));
    expect(classSet(slot as Element)).toEqual([...ACTIONS_CLASSES].toSorted());
    expect(slot?.querySelector('[data-testid="apps-register-link"]')).not.toBeNull();
  });

  it("omits the actions slot when no actions are given", async () => {
    const { container } = await render(
      <PageHeader title="Organizations" description="The organizations you belong to." />,
    );

    // The organizations route's shape: a heading row with no CTA beside it.
    expect(rootOf(container).children.length).toBe(1);
  });

  it("renders all three parts together", async () => {
    const { container } = await render(
      <PageHeader
        title="My Apps"
        description="Everything you have registered."
        actions={<button type="button">Register New App</button>}
      />,
    );

    const root = rootOf(container);
    expect(root.children.length).toBe(2);
    expect(headingOf(container).textContent).toBe("My Apps");
    expect(descriptionOf(container).textContent).toBe("Everything you have registered.");
    expect(root.lastElementChild?.textContent).toBe("Register New App");
  });

  it("lets a caller className override the recipe's bottom margin", async () => {
    const { container } = await render(<PageHeader title="My Apps" className="mb-0" />);

    const root = rootOf(container);
    expect(root.classList.contains("mb-0")).toBe(true);
    expect(root.classList.contains("mb-8")).toBe(false);
    expect(root.classList.contains("justify-between")).toBe(true);
  });

  it("passes an app-owned data-testid and rest props through to the root", async () => {
    const { container } = await render(
      <PageHeader data-testid="apps-header" id="apps-header-row" title="My Apps" />,
    );

    const root = rootOf(container);
    expect(root.getAttribute("data-testid")).toBe("apps-header");
    expect(root.id).toBe("apps-header-row");
  });

  it("keeps a string title out of the root's title attribute", async () => {
    const { container } = await render(<PageHeader title="My Apps" />);

    // `title` is content, not a tooltip — `HTMLAttributes`' own `title` is
    // omitted from the prop type, so nothing leaks onto the element.
    expect(rootOf(container).hasAttribute("title")).toBe(false);
  });

  it("derives the inner test ids from the root's data-testid", async () => {
    const { container } = await render(
      <PageHeader
        data-testid="apps-header"
        title="My Apps"
        description="Everything you have registered."
        actions={<button type="button">Register New App</button>}
      />,
    );

    // Derived, never hand-passed — the same rule packages/forms applies to a
    // field catalog, so an app names the block once and its E2E selectors follow.
    expect(headingOf(container).getAttribute("data-testid")).toBe("apps-header-title");
    expect(descriptionOf(container).getAttribute("data-testid")).toBe("apps-header-description");
    expect(rootOf(container).lastElementChild?.getAttribute("data-testid")).toBe(
      "apps-header-actions",
    );
  });

  it("stamps no inner test ids when the root carries none", async () => {
    const { container } = await render(
      <PageHeader
        title="My Apps"
        description="Everything you have registered."
        actions={<button type="button">Register New App</button>}
      />,
    );

    expect(headingOf(container).hasAttribute("data-testid")).toBe(false);
    expect(descriptionOf(container).hasAttribute("data-testid")).toBe(false);
    expect(rootOf(container).lastElementChild?.hasAttribute("data-testid")).toBe(false);
  });

  it("renders a ReactNode title, not just a string", async () => {
    const { container } = await render(
      <PageHeader
        title={
          <>
            My Apps <span data-testid="apps-header-count">(3)</span>
          </>
        }
      />,
    );

    const heading = headingOf(container);
    expect(heading.querySelector('[data-testid="apps-header-count"]')).not.toBeNull();
    // Still one heading carrying the whole title, not a nested second element.
    expect(container.querySelectorAll("h1").length).toBe(1);
  });
});
