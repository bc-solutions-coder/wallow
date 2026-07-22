import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { Field } from "./field";

describe("Field", () => {
  it("renders the space-y-2 row wrapper around its children", async () => {
    const { container } = await render(
      <Field>
        <span data-testid="child" />
      </Field>,
    );

    const row = container.firstElementChild as HTMLElement | null;
    expect(row).not.toBeNull();
    expect((row as HTMLElement).className).toBe("space-y-2");
    expect((row as HTMLElement).querySelector('[data-testid="child"]')).not.toBeNull();
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Field data-testid="login-email-field" />);

    expect(container.querySelector('[data-testid="login-email-field"]')).not.toBeNull();
  });
});
