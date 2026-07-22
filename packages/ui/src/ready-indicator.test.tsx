import { render } from "@bc-solutions-coder/testing/render";
import { describe, expect, it, vi } from "vitest";

import { READY_ATTRIBUTE, ReadyIndicator } from "./ready-indicator";

/**
 * The generalized spec for the shared ReadyIndicator (Wallow-0q2s.6.2). It merges
 * the two near-identical per-app specs (apps/{wallow-auth,wallow-web}) into one
 * suite parameterized over the injected `testId` prop, proving the prop drives the
 * `data-testid` the two app copies previously hard-coded (`auth-ready`/`web-ready`).
 *
 * Assertions go through `document.body.matches(selector)` rather than
 * `getAttribute`, because the selector is the actual contract: E2E waits on
 * `[data-app-ready='true']` exactly as it waited on `[data-blazor-ready='true']`.
 * `vi.waitFor` wraps the post-mount assertions defensively (auth's spec did; web's
 * did not) so the effect's commit is observed rather than assumed.
 */
const readySelector = `[${READY_ATTRIBUTE}="true"]`;

describe.each(["auth-ready", "web-ready"])("ReadyIndicator (testId=%s)", (testId: string) => {
  it("marks the document ready once mounted", async () => {
    await render(<ReadyIndicator testId={testId} />);

    await vi.waitFor(() => {
      expect(document.body.matches(readySelector)).toBe(true);
    });
    expect(document.body.matches(`[data-testid="${testId}"]`)).toBe(true);
  });

  it("renders no markup of its own", async () => {
    const { container } = await render(<ReadyIndicator testId={testId} />);

    expect(container.innerHTML).toBe("");
  });

  it("does not mark the document ready when server-rendered", async () => {
    // The signal must mean "hydrated and interactive", so it must not appear in
    // SSR output — the same guarantee BlazorReadyIndicator gets by only firing
    // from OnAfterRenderAsync. renderToString runs no effects, so nothing is set.
    const { renderToString } = await import("react-dom/server");

    const html: string = renderToString(<ReadyIndicator testId={testId} />);

    expect(html).toBe("");
    expect(document.body.matches(`[${READY_ATTRIBUTE}]`)).toBe(false);
  });

  it("clears the signal when the app unmounts", async () => {
    const { unmount } = await render(<ReadyIndicator testId={testId} />);
    await vi.waitFor(() => {
      expect(document.body.matches(readySelector)).toBe(true);
    });

    await unmount();

    expect(document.body.matches(`[${READY_ATTRIBUTE}]`)).toBe(false);
    expect(document.body.matches("[data-testid]")).toBe(false);
  });
});
