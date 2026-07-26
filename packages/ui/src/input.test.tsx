import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { Input } from "./input";

const INPUT_RECIPE =
  "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground";

function onlyInput(container: HTMLElement): HTMLInputElement {
  const input = container.querySelector("input");
  expect(input).not.toBeNull();
  return input as HTMLInputElement;
}

describe("Input", () => {
  it("renders the exact input recipe by default", async () => {
    const { container } = await render(<Input />);

    expect(onlyInput(container).className).toBe(INPUT_RECIPE);
  });

  it("forwards native input attributes (type, placeholder, id)", async () => {
    const { container } = await render(
      <Input id="email" type="email" placeholder="name@example.com" />,
    );

    const input = onlyInput(container);
    expect(input.id).toBe("email");
    expect(input.type).toBe("email");
    expect(input.placeholder).toBe("name@example.com");
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Input data-testid="login-email" />);

    expect(container.querySelector('[data-testid="login-email"]')).not.toBeNull();
  });

  it("appends a caller-supplied className to the recipe", async () => {
    const { container } = await render(<Input className="h-4 w-4" />);

    const input = onlyInput(container);
    expect(input.classList.contains("h-4")).toBe(true);
    expect(input.classList.contains("rounded-md")).toBe(true);
  });
});
