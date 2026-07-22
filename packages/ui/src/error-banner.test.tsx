import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { ErrorBanner } from "./error-banner";

describe("ErrorBanner", () => {
  it("renders the wrapper recipe around a destructive-text paragraph", async () => {
    const { container } = await render(<ErrorBanner>Invalid credentials</ErrorBanner>);

    const wrapper = container.firstElementChild as HTMLElement | null;
    expect(wrapper).not.toBeNull();
    expect((wrapper as HTMLElement).className).toBe(
      "rounded-md border border-destructive bg-destructive/10 p-3",
    );

    const paragraph = (wrapper as HTMLElement).querySelector("p");
    expect(paragraph).not.toBeNull();
    expect((paragraph as HTMLParagraphElement).className).toBe("text-sm text-destructive");
    expect((paragraph as HTMLParagraphElement).textContent).toBe("Invalid credentials");
  });

  it("passes through an app-owned data-testid onto the wrapper", async () => {
    const { container } = await render(<ErrorBanner data-testid="login-error">boom</ErrorBanner>);

    const tagged = container.querySelector('[data-testid="login-error"]');
    expect(tagged).not.toBeNull();
    expect((tagged as HTMLElement).classList.contains("border-destructive")).toBe(true);
  });
});
