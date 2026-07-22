import { render } from "@bc-solutions-coder/testing/render";
import { page } from "vitest/browser";
import { describe, expect, it } from "vitest";

import { HelloCard } from "./HelloCard";

/**
 * Component spec for the minimal app's only screen. Runs in headless Chromium
 * (Vitest browser mode) via the shared `render` helper from
 * `@bc-solutions-coder/testing/render` — the same real-browser harness the
 * product apps use, exercised here to prove the testing package is wired.
 */
describe("HelloCard", () => {
  it("renders the heading and body composed from ui primitives", async () => {
    render(<HelloCard />);

    await expect.element(page.getByTestId("hello-heading")).toBeVisible();
    await expect.element(page.getByTestId("hello-body")).toBeVisible();
    await expect.element(page.getByTestId("hello-attribution")).toBeVisible();
  });
});
