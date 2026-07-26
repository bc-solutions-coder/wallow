import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { MutedText } from "./muted-text";

function onlyParagraph(container: HTMLElement): HTMLParagraphElement {
  const p = container.querySelector("p");
  expect(p).not.toBeNull();
  return p as HTMLParagraphElement;
}

describe("MutedText", () => {
  it("renders a paragraph with the exact muted recipe and its children", async () => {
    const { container } = await render(<MutedText>Forgot password?</MutedText>);

    const p = onlyParagraph(container);
    expect(p.className).toBe("text-sm text-muted-foreground");
    expect(p.textContent).toBe("Forgot password?");
  });

  it("appends a caller-supplied className to the recipe", async () => {
    const { container } = await render(<MutedText className="mt-1">Tagline</MutedText>);

    const p = onlyParagraph(container);
    expect(p.classList.contains("mt-1")).toBe(true);
    expect(p.classList.contains("text-muted-foreground")).toBe(true);
  });

  it("passes through an app-owned data-testid", async () => {
    const { container } = await render(<MutedText data-testid="login-hint">hint</MutedText>);

    expect(container.querySelector('[data-testid="login-hint"]')).not.toBeNull();
  });
});
