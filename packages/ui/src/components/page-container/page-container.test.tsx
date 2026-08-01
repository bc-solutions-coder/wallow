import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { PageContainer } from "./page-container";

/*
 * The edges the story cannot show: what a caller `className` does to the width.
 * `cn()` merges the caller over the recipe, so a page adding rhythm keeps the
 * shared width while a page naming its own `max-w-*` replaces it — the second is
 * the escape hatch, and a page reaching for it is opting OUT of the one rule.
 *
 * Class-level rather than computed, because the `browser` project loads no
 * Tailwind and every width would measure the same.
 */

function columnOf(container: HTMLElement): HTMLElement {
  const column = container.firstElementChild;
  expect(column).not.toBeNull();
  return column as HTMLElement;
}

describe("PageContainer", () => {
  it("renders its children inside the shared column", async () => {
    const { container } = await render(
      <PageContainer data-testid="dashboard-apps">
        <span data-testid="child" />
      </PageContainer>,
    );

    const column = columnOf(container);
    expect(column.getAttribute("data-testid")).toBe("dashboard-apps");
    expect([...column.classList].toSorted()).toEqual(["max-w-5xl", "mx-auto"]);
    expect(column.querySelector('[data-testid="child"]')).not.toBeNull();
  });

  it("keeps the shared width when a caller adds unrelated utilities", async () => {
    const { container } = await render(<PageContainer className="space-y-8" />);

    expect([...columnOf(container).classList].toSorted()).toEqual([
      "max-w-5xl",
      "mx-auto",
      "space-y-8",
    ]);
  });

  it("lets a caller className override the width outright", async () => {
    const { container } = await render(<PageContainer className="max-w-2xl" />);

    const column = columnOf(container);
    expect(column.classList.contains("max-w-2xl")).toBe(true);
    expect(column.classList.contains("max-w-5xl")).toBe(false);
    expect(column.classList.contains("mx-auto")).toBe(true);
  });
});
