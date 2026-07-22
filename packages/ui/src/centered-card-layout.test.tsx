import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it } from "vitest";

import { CenteredCardLayout } from "./centered-card-layout";

describe("CenteredCardLayout", () => {
  it("renders the centred viewport shell around a fixed-width column", async () => {
    const { container } = await render(
      <CenteredCardLayout>
        <span data-testid="child" />
      </CenteredCardLayout>,
    );

    const shell = container.firstElementChild as HTMLElement | null;
    expect(shell).not.toBeNull();
    expect((shell as HTMLElement).className).toBe(
      "min-h-screen bg-background flex flex-col items-center justify-center px-4",
    );

    const column = (shell as HTMLElement).firstElementChild as HTMLElement | null;
    expect(column).not.toBeNull();
    expect((column as HTMLElement).className).toBe("w-full max-w-[420px]");
    expect((column as HTMLElement).querySelector('[data-testid="child"]')).not.toBeNull();
  });

  it("passes through an app-owned data-testid onto the inner column", async () => {
    const { container } = await render(<CenteredCardLayout data-testid="auth-column" />);

    const tagged = container.querySelector('[data-testid="auth-column"]');
    expect(tagged).not.toBeNull();
    expect((tagged as HTMLElement).classList.contains("max-w-[420px]")).toBe(true);
  });
});
