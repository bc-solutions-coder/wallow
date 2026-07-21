import { render } from "vitest-browser-react";
import { describe, expect, it, vi } from "vitest";

import { READY_ATTRIBUTE, READY_TEST_ID, ReadyIndicator } from "./ready-indicator";

/**
 * Assertions go through `document.body.matches(selector)` rather than
 * `getAttribute`, because the selector is the actual contract: E2E waits on
 * `[data-app-ready='true']` exactly as it waited on `[data-blazor-ready='true']`.
 * Matching the selector proves the rendered attribute, not just the dataset key
 * the implementation happens to write through.
 */
const readySelector = `[${READY_ATTRIBUTE}="true"]`;

describe("ReadyIndicator", () => {
  it("marks the document ready once mounted", async () => {
    await render(<ReadyIndicator />);

    await vi.waitFor(() => {
      expect(document.body.matches(readySelector)).toBe(true);
    });
    expect(document.body.matches(`[data-testid="${READY_TEST_ID}"]`)).toBe(true);
  });

  it("renders no markup of its own", async () => {
    const { container } = await render(<ReadyIndicator />);

    expect(container.innerHTML).toBe("");
  });

  it("does not mark the document ready when server-rendered", async () => {
    // The signal must mean "hydrated and interactive", so it must not appear in
    // SSR output — the same guarantee BlazorReadyIndicator gets by only firing
    // from OnAfterRenderAsync. renderToString runs no effects, so nothing is set.
    const { renderToString } = await import("react-dom/server");

    const html: string = renderToString(<ReadyIndicator />);

    expect(html).toBe("");
    expect(document.body.matches(`[${READY_ATTRIBUTE}]`)).toBe(false);
  });

  it("clears the signal when the app unmounts", async () => {
    const { unmount } = await render(<ReadyIndicator />);
    await vi.waitFor(() => {
      expect(document.body.matches(readySelector)).toBe(true);
    });

    await unmount();

    expect(document.body.matches(`[${READY_ATTRIBUTE}]`)).toBe(false);
    expect(document.body.matches("[data-testid]")).toBe(false);
  });
});
