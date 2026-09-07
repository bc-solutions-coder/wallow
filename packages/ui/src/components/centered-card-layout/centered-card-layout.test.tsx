import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { CenteredCardLayout } from "./centered-card-layout";

/*
 * The viewport shell stays sealed — a caller styles the column, never the
 * wrapper — which is why this is two recipes rather than one.
 *
 * Class assertions are order-free sets, per the Button exemplar.
 */

/** The full-viewport centring wrapper. */
const VIEWPORT_CLASSES = [
  "min-h-screen",
  "bg-background",
  "flex",
  "flex-col",
  "items-center",
  "justify-center",
  "px-4",
];

/** The fixed-width column the caller's props land on. */
const COLUMN_CLASSES = ["w-full", "max-w-[420px]"];

/** The element's classes as an order-free set, so tailwind-merge may reorder. */
function classSet(element: Element): string[] {
  return [...element.classList].toSorted();
}

function shellOf(container: HTMLElement): HTMLElement {
  const shell = container.firstElementChild;
  expect(shell).not.toBeNull();
  return shell as HTMLElement;
}

function columnOf(container: HTMLElement): HTMLElement {
  const column = shellOf(container).firstElementChild;
  expect(column).not.toBeNull();
  return column as HTMLElement;
}

describe("CenteredCardLayout", () => {
  it("renders the centred viewport shell around a fixed-width column", async () => {
    const { container } = await render(
      <CenteredCardLayout>
        <span data-testid="child" />
      </CenteredCardLayout>,
    );

    expect(classSet(shellOf(container))).toEqual([...VIEWPORT_CLASSES].toSorted());

    const column = columnOf(container);
    expect(classSet(column)).toEqual([...COLUMN_CLASSES].toSorted());
    expect(column.querySelector('[data-testid="child"]')).not.toBeNull();
  });

  it("passes through an app-owned data-testid onto the inner column", async () => {
    const { container } = await render(<CenteredCardLayout data-testid="auth-column" />);

    const tagged = container.querySelector('[data-testid="auth-column"]');
    expect(tagged).not.toBeNull();
    expect((tagged as HTMLElement).classList.contains("max-w-[420px]")).toBe(true);
  });

  it("merges a caller className onto the column instead of discarding it", async () => {
    const { container } = await render(<CenteredCardLayout className="gap-4" />);

    expect(classSet(columnOf(container))).toEqual([...COLUMN_CLASSES, "gap-4"].toSorted());
  });

  it("lets a caller className override the column width", async () => {
    const { container } = await render(<CenteredCardLayout className="max-w-2xl" />);

    const column = columnOf(container);
    expect(column.classList.contains("max-w-2xl")).toBe(true);
    expect(column.classList.contains("max-w-[420px]")).toBe(false);
    expect(column.classList.contains("w-full")).toBe(true);
  });

  it("keeps the viewport shell sealed against a caller className", async () => {
    const { container } = await render(<CenteredCardLayout className="px-12" />);

    expect(classSet(shellOf(container))).toEqual([...VIEWPORT_CLASSES].toSorted());
  });
});
