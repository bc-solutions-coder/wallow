// @vitest-environment jsdom
import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";

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
  it("marks the document ready once mounted", () => {
    render(<ReadyIndicator />);

    expect(document.body.matches(readySelector)).toBe(true);
    expect(document.body.matches(`[data-testid="${READY_TEST_ID}"]`)).toBe(true);
  });

  it("renders no markup of its own", () => {
    const { container } = render(<ReadyIndicator />);

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

  it("clears the signal when the app unmounts", () => {
    const { unmount } = render(<ReadyIndicator />);
    expect(document.body.matches(readySelector)).toBe(true);

    unmount();

    expect(document.body.matches(`[${READY_ATTRIBUTE}]`)).toBe(false);
    expect(document.body.matches("[data-testid]")).toBe(false);
  });
});
