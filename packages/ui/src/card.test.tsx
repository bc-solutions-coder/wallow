import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { Card, CardTitle } from "./card";

function firstChild(container: HTMLElement): HTMLElement {
  const el = container.firstElementChild;
  expect(el).not.toBeNull();
  return el as HTMLElement;
}

describe("Card", () => {
  it("renders the dominant recipe (p-6 space-y-6) by default", async () => {
    const { container } = await render(
      <Card>
        <span data-testid="child" />
      </Card>,
    );

    const card = firstChild(container);
    expect(card.className).toBe("rounded-lg border border-border bg-card p-6 space-y-6");
    expect(card.querySelector('[data-testid="child"]')).not.toBeNull();
  });

  it("covers the LoginScreen outlier via the spacing override", async () => {
    const { container } = await render(<Card spacing="p-6 space-y-4" />);

    expect(firstChild(container).className).toBe(
      "rounded-lg border border-border bg-card p-6 space-y-4",
    );
  });

  it("covers the RegisterForm bare-padding outlier via the spacing override", async () => {
    const { container } = await render(<Card spacing="p-6" />);

    expect(firstChild(container).className).toBe("rounded-lg border border-border bg-card p-6");
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<Card data-testid="login-card" />);

    expect(container.querySelector('[data-testid="login-card"]')).not.toBeNull();
  });
});

describe("CardTitle", () => {
  it("renders an h2 with the exact title recipe and its children", async () => {
    const { container } = await render(<CardTitle>Sign in</CardTitle>);

    const heading = container.querySelector("h2");
    expect(heading).not.toBeNull();
    expect((heading as HTMLHeadingElement).className).toBe(
      "text-lg font-semibold text-card-foreground",
    );
    expect((heading as HTMLHeadingElement).textContent).toBe("Sign in");
  });
});
