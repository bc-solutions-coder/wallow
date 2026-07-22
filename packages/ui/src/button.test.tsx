import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { Button } from "./button";

/**
 * Browser-mode spec for the shared Button (Wallow-0q2s.6.4). The `primary`
 * default pins the exact measured recipe (11x in wallow-auth) so 6.5 can refactor
 * onto it with byte-identical rendered classes; the variant swaps assert only the
 * colour-pair tokens so the implementation keeps freedom over class ordering.
 */
const PRIMARY_RECIPE =
  "w-full rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50";

function onlyButton(container: HTMLElement): HTMLButtonElement {
  const button = container.querySelector("button");
  expect(button).not.toBeNull();
  return button as HTMLButtonElement;
}

describe("Button", () => {
  it("renders the exact primary recipe by default", async () => {
    const { container } = await render(<Button>Sign in</Button>);

    const button = onlyButton(container);
    expect(button.className).toBe(PRIMARY_RECIPE);
    expect(button.textContent).toBe("Sign in");
  });

  it("swaps to the secondary colour pair", async () => {
    const { container } = await render(<Button variant="secondary">Cancel</Button>);

    const button = onlyButton(container);
    expect(button.classList.contains("bg-secondary")).toBe(true);
    expect(button.classList.contains("text-secondary-foreground")).toBe(true);
    expect(button.classList.contains("bg-primary")).toBe(false);
  });

  it("swaps to the destructive colour pair", async () => {
    const { container } = await render(<Button variant="destructive">Delete</Button>);

    const button = onlyButton(container);
    expect(button.classList.contains("bg-destructive")).toBe(true);
    expect(button.classList.contains("text-destructive-foreground")).toBe(true);
    expect(button.classList.contains("bg-primary")).toBe(false);
  });

  it("forwards native button attributes (type, disabled)", async () => {
    const { container } = await render(
      <Button type="submit" disabled>
        Submit
      </Button>,
    );

    const button = onlyButton(container);
    expect(button.type).toBe("submit");
    expect(button.disabled).toBe(true);
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Button data-testid="login-submit">Sign in</Button>);

    expect(container.querySelector('[data-testid="login-submit"]')).not.toBeNull();
  });

  it("appends a caller-supplied className to the recipe", async () => {
    const { container } = await render(<Button className="flex-1">Sign in</Button>);

    const button = onlyButton(container);
    expect(button.classList.contains("flex-1")).toBe(true);
    expect(button.classList.contains("bg-primary")).toBe(true);
  });
});
