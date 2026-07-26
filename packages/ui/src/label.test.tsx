import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { Label } from "./label";

const LABEL_RECIPE = "text-sm font-medium text-foreground";

function onlyLabel(container: HTMLElement): HTMLLabelElement {
  const label = container.querySelector("label");
  expect(label).not.toBeNull();
  return label as HTMLLabelElement;
}

describe("Label", () => {
  it("renders the exact label recipe with its children", async () => {
    const { container } = await render(<Label>Email</Label>);

    const label = onlyLabel(container);
    expect(label.className).toBe(LABEL_RECIPE);
    expect(label.textContent).toBe("Email");
  });

  it("forwards htmlFor onto the label's for attribute", async () => {
    const { container } = await render(<Label htmlFor="email">Email</Label>);

    expect(onlyLabel(container).htmlFor).toBe("email");
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Label data-testid="login-email-label">Email</Label>);

    expect(container.querySelector('[data-testid="login-email-label"]')).not.toBeNull();
  });
});
